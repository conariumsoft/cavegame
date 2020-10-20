﻿//#define SERVER

using Cave;
using CaveGame.Client.UI;
using CaveGame.Core;
using CaveGame.Core.Entities;
using CaveGame.Core.Furniture;
using CaveGame.Core.Generic;
using CaveGame.Core.Inventory;
using CaveGame.Core.Network;
using CaveGame.Core.Particles;
using CaveGame.Core.Tiles;
using CaveGame.Core.Walls;
using CaveGame.Server;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using System;
using System.Diagnostics;
using System.Threading;

namespace CaveGame.Client
{
	public class TempItemDef
	{
		public Rectangle Quad;
		public byte TileID;
		public Color Color;
	}

	// The testbench for building the server/client archite ture
	public class Hotbar
	{
		

		public int Index { get; set; }

		public Item[] ItemSet =
		{
			new TileItem(new Core.Tiles.Stone()),
			new TileItem(new Grass()),
			new TileItem(new Core.Tiles. Dirt()),
			new TileItem(new Leaves()),
			new TileItem(new Core.Tiles.ClayBrick()),
			new TileItem(new Core.Tiles.StoneBrick()),
			new TileItem(new Torch()),
			new TileItem(new RedTorch()),
			new TileItem(new BlueTorch()),
			new TileItem(new GreenTorch()),
			new TileItem(new YellowTorch()),
			new TileItem(new Core.Tiles.OakPlank()),
			new TileItem(new CopperOre()),
			new TileItem(new UraniumOre()),
			new TileItem(new CobaltOre()),
			new TileItem(new GoldOre()),
			new TileItem(new IronOre()),
			new TileItem(new TNT()),
			new TileItem(new Platform()),
			new TileItem(new Rope()),
			new TileItem(new Water()),
			new TileItem(new Lava()),
			new TileItem(new Sludge()),
			new WallItem(new Core.Walls.OakPlank()),
			new WallItem(new Core.Walls.StoneBrick()),
			new WallItem(new Core.Walls.CarvedStoneBrick()),
			new WallItem(new Core.Walls.CarvedSandstoneBrick()),
			new WallItem(new Core.Walls.MossyStoneBrick()),
			new BombItem(),
			new DoorItem(),
			new WorkbenchItem(),
			new TileRemoverItem(),
			new WallRemoverItem(),
			
		};

		private int lastScroll = 0;

		public void Update(GameTime gt)
		{
			MouseState mouse = Mouse.GetState();

			
			var scroll = (mouse.ScrollWheelValue / 120) - (lastScroll/120); // why is MouseScroll in 120 increments???
			lastScroll = mouse.ScrollWheelValue;

			Index -= scroll;
			Index = Index.Mod(ItemSet.Length);

		}


		public void Draw(SpriteBatch sb)
		{
			sb.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp);
			int travel = 0;

			Vector2 pos = new Vector2(GameGlobals.Width - (30* ItemSet.Length), GameGlobals.Height - 30);

			for (int i = 0; i < ItemSet.Length; i++)
			{
				if (Index == i)
				{
					sb.Rect(Color.Gray, pos+new Vector2(travel, -4), new Vector2(32, 32));
					ItemSet[i].Draw(sb, pos + new Vector2(travel, -4), 2f);
					travel += 32;
				}
				else
				{
					sb.Rect(Color.Gray, pos + new Vector2(travel, 0), new Vector2(24, 24));
					ItemSet[i].Draw(sb, pos + new Vector2(travel, 0), 1.5f);
					travel += 24;
				}
			}

			sb.End();
		}
	}


	

	public class GameClient : IGameContext, IGameClient
	{
		public bool ShowChunkBoundaries { get; set; }

		public static float CameraZoom = 2.0f;

		public CaveGameGL Game { get; private set; }

		public GameChat Chat { get; private set; }

		public string NetworkUsername { get; set; }
		public string ConnectAddress { get; set; }

		public bool Active { get; set; }

		Game IGameContext.Game => Game;
		IGameWorld IGameClient.World => World;

		public Hotbar Hotbar { get; set; }

		public LocalWorld World { get; private set; }


		private NetworkClient gameClient;

		public Camera2D Camera { get; }

		int MyUserID;
		int MyPlayerID;

		public LocalPlayer myPlayer;

		private DelayedTask chunkUnloadingTask;
		DelayedTask playerStateReplicationTask;
		DelayedTask chunkLoadingTask;

		private int IntitalScrollValue;

		public GameClient(CaveGameGL _game)
		{
			Game = _game;

			MouseState mouse = Mouse.GetState();


			World = new LocalWorld();
			Camera = new Camera2D(Game.GraphicsDevice.Viewport) { Zoom = CameraZoom };
			Chat = new GameChat(this);
			IntitalScrollValue = mouse.ScrollWheelValue;

			playerStateReplicationTask = new DelayedTask(ReplicatePlayerState, 1 / 10.0f);
			chunkUnloadingTask = new DelayedTask(ChunkUnloadingCheck, 1/2.0f);
			chunkLoadingTask = new DelayedTask(ChunkLoadingCheckUpdate, 1 / 5.0f);
		}

		public void Send(Packet p)
		{
			gameClient.SendPacket(p);
		}

		public void SendChatMessage(object sender, string message)
		{
			Chat.Open = false;
			gameClient.SendPacket(new ClientChatMessagePacket(message));	
		}
		private IEntity FindEntityOfID(int id)
		{
			foreach (var entity in World.Entities)
				if (entity.EntityNetworkID == id)
					return entity;
			return null;
		}
		public void Disconnect()
		{
			if (myPlayer != null)
			{
				gameClient.SendPacket(new QuitPacket(myPlayer.EntityNetworkID));
			}

			Thread.Sleep(50);
			//gameClient.Stop();
		}


		private void ChunkUnloadingCheck()
		{

			foreach (var chunkpair in World.Chunks)
			{
				if (!World.LoadedChunks.Contains(chunkpair.Key))
				{
					if (World.Chunks.ContainsKey(chunkpair.Key))
					{
						World.Chunks.TryRemove(chunkpair.Key, out _);
						World.Lighting.UnregisterChunk(chunkpair.Key);
					}
				}
			}
			//	}
		}

		private void ChunkLoadingCheckUpdate()
		{
			if (myPlayer!=null)
			{

			
			int camChunkX = (int)Math.Floor(myPlayer.Position.X / (Globals.ChunkSize * Globals.TileSize));
			int camChunkY = (int)Math.Floor(myPlayer.Position.Y / (Globals.ChunkSize * Globals.TileSize));

			int rendr = 1;
			World.LoadedChunks.Clear();
			for (int x = -rendr; x < rendr + 1; x++)
			{
				for (int y = -rendr; y < rendr + 1; y++)
				{
					ChunkCoordinates chunkCoords = new ChunkCoordinates(camChunkX + x, camChunkY + y);

					World.LoadedChunks.Add(chunkCoords);// TODO: spooky action at a distance?


					if ((!World.Chunks.ContainsKey(chunkCoords)) && (!World.RequestedChunks.Contains(chunkCoords)))
					{
						gameClient.SendPacket(new RequestChunkPacket(chunkCoords));
						World.RequestedChunks.Add(chunkCoords);
					}
				}
			}
			}
		}

		#region NetworkListenerMethods


		private void DownloadChunk(NetworkMessage message)
		{
			ChunkDownloadPacket chunkdata = new ChunkDownloadPacket(message.Packet.GetBytes());

			Chunk chunk = chunkdata.StoredChunk;

			// Did we ask for this chunk?
			if (World.RequestedChunks.Contains(chunk.Coordinates))
			{
				//World.Chunks. Add(chunk.Coordinates, chunk);
				World.Chunks.TryAdd(chunk.Coordinates, chunk);
				World.RequestedChunks.Remove(chunk.Coordinates);
				World.Lighting.RegisterChunk(chunk);
			}
			
		}
		private void UpdateTile(NetworkMessage message)
		{
			PlaceTilePacket packet = new PlaceTilePacket(message.Packet.GetBytes());

			Tile t = Tile.FromID(packet.TileID);


			//Debug.WriteLine(String.Format("CTile {0} {1} id {2}", packet.WorldX, packet.WorldY, packet.TileID));
			t.Damage = packet.Damage;
			t.TileState = packet.TileState;
			World.SetTile(packet.WorldX, packet.WorldY, t);
		}
		private void UpdateWall(NetworkMessage message)
		{
			PlaceWallPacket packet = new PlaceWallPacket(message.Packet.GetBytes());

			Wall w = Wall.FromID(packet.WallID);

			//Debug.WriteLine("YEEEEEEEEE");
			//Debug.WriteLine(String.Format("CTile {0} {1} id {2}", packet.WorldX, packet.WorldY, packet.TileID));
			w.Damage = packet.Damage;
			//t.TileState = packet.TileState;
			World.SetWall(packet.WorldX, packet.WorldY, w);
		}
		private void OnPeerJoined(NetworkMessage message)
		{
			PlayerJoinedPacket packet = new PlayerJoinedPacket(message.Packet.GetBytes());

			var player = new PeerPlayer()
			{
				EntityNetworkID = packet.EntityID,
				Color = packet.PlayerColor,
				DisplayName = packet.Username,
				RemoteControlled = true
			};
			World.Entities.Add(player);
		}
		private void OnPeerLeft(NetworkMessage message)
		{
			PlayerLeftPacket packet = new PlayerLeftPacket(message.Packet.GetBytes());

			var entity = FindEntityOfID(packet.EntityID);

			if (entity != null)
			{
				World.Entities.Remove(entity);
			}
		}
		private void OnEntityPosition(NetworkMessage message)
		{
			EntityPositionPacket packet = new EntityPositionPacket(message.Packet.GetBytes());

			var entity = FindEntityOfID(packet.EntityID);

			if (entity == null)
				return;



			if (entity is Player plr )
			{
				if (packet.EntityID == MyPlayerID)
					return;

				plr.NextPosition = new Vector2(packet.NextX, packet.NextY);
				return;
			}
	

			if (entity is IPositional positional)
				positional.Position = new Vector2(packet.PosX, packet.PosY);

			if (entity is IVelocity velocity)
				velocity.Velocity = new Vector2(packet.VelX, packet.VelY);

			if (entity is INextPosition next)
				next.NextPosition = new Vector2(packet.NextX, packet.NextY);
		}
		private void OnChatMessage(NetworkMessage message)
		{
			ServerChatMessagePacket packet = new ServerChatMessagePacket(message.Packet.GetBytes());
			Chat.AddMessage(packet.Message, packet.TextColor);
		}
		private void OnPlayerState(NetworkMessage message)
		{
			PlayerStatePacket packet = new PlayerStatePacket(message.Packet.GetBytes());

			IEntity ent = FindEntityOfID(packet.EntityID);

			if (ent is Player plr)
			{
				plr.Walking = packet.Walking;
				plr.OnGround = packet.OnGround;
				plr.Facing = packet.Facing;
			}
		}
		private void OnServerDenyJoining(NetworkMessage message)
		{
			RejectJoinPacket packet = new RejectJoinPacket(message.Packet.GetBytes());
			// TODO: send player to the rejection screen

			Game.CurrentGameContext = Game.ServerKickedContext;
			Game.ServerKickedContext.Message = packet.RejectReason;
		}
		private void OnServerAcceptJoining(NetworkMessage message)
		{
			AcceptJoinPacket packet = new AcceptJoinPacket(message.Packet.GetBytes());


			MyUserID = packet.YourUserNetworkID;
			MyPlayerID = packet.YourPlayerNetworkID;

			// create me player
			LocalPlayer myplayer = new LocalPlayer();
			myplayer.EntityNetworkID = MyPlayerID;
			myPlayer = myplayer;
			World.Entities.Add(myplayer);
		}

		Random r = new Random();
		private void OnExplosion(NetworkMessage msg)
		{
			ExplosionPacket packet = new ExplosionPacket(msg.Packet.GetBytes());

			Vector2 pos = new Vector2(packet.X, packet.Y);

			for (int i = 0; i<360; i+=10)
			{
				float randy = r.Next(0, 20)-10;
				Rotation rotation = Rotation.FromDeg(i+randy);
				Vector2 direction = new Vector2((float)Math.Sin(rotation.Radians), (float)Math.Cos(rotation.Radians));
				float size = ((float)r.NextDouble() *0.25f) + (packet.Strength*0.25f);
				World.ParticleSystem.Add(new SmokeParticle(pos, Color.White, Rotation.FromDeg(0), size, direction* (packet.Radius*0.4f+((float)r.NextDouble()))));
			}
		}

		private void OnBombSpawned(NetworkMessage message)
		{
			
			SpawnBombEntityPacket packet = new SpawnBombEntityPacket(message.Packet.GetBytes());
			Core.Entities.Bomb b = new Core.Entities.Bomb();
			b.EntityNetworkID = packet.EntityNetworkID;
			b.RemoteControlled = true;
			World.Entities.Add(b);
		}

		private void OnDestroyEntity(NetworkMessage message)
		{
			DropEntityPacket packet = new DropEntityPacket(message.Packet.GetBytes());
			var entity = FindEntityOfID(packet.EntityID);

			World.Entities.Remove(entity);

		}

		private void OnPlaceFurniture(NetworkMessage msg)
		{
			PlaceFurniturePacket packet = new PlaceFurniturePacket(msg.Packet.GetBytes());

			FurnitureTile f = FurnitureTile.FromID(packet.FurnitureID);
			f.FurnitureNetworkID = packet.NetworkID;
			f.Position = new Point(packet.WorldX, packet.WorldY);
			World.Furniture.Add(f);
			Debug.WriteLine("Place {0}", packet.NetworkID);

		}
		private void OnRemoveFurniture(NetworkMessage msg)
		{
			RemoveFurniturePacket packet = new RemoveFurniturePacket(msg.Packet.GetBytes());

			var possibleF = World.GetFurniture(packet.FurnitureNetworkID);
			Debug.WriteLine("REMOVE {0}", packet.FurnitureNetworkID);
			if (possibleF != null)
				World.Furniture.Remove(possibleF);
				

		}

		private void OnPlayerClosesDoor(NetworkMessage msg)
		{
			CloseDoorPacket packet = new CloseDoorPacket(msg.Packet.GetBytes());

			var possibleF = World.GetFurniture(packet.FurnitureNetworkID);
			if (possibleF != null && possibleF is WoodenDoor door)
				door.State = DoorState.Closed;


		}

		private void OnPlayerOpensDoor(NetworkMessage msg)
		{
			OpenDoorPacket packet = new OpenDoorPacket(msg.Packet.GetBytes());

			var possibleF = World.GetFurniture(packet.FurnitureNetworkID);
			if (possibleF != null && possibleF is WoodenDoor door)
			{
				if (packet.Direction == HorizontalDirection.Left)
					door.State = DoorState.OpenLeft;
				if (packet.Direction == HorizontalDirection.Right)
					door.State = DoorState.OpenRight;
			}
		}
		private void OnServerChangedTimeOfDay(NetworkMessage msg)
		{
			TimeOfDayPacket packet = new TimeOfDayPacket(msg.Packet.GetBytes());

			World.TimeOfDay = packet.Time;
		}
				

		#endregion
		// gameclient
		private void ReadIncomingPackets()
		{
			while (gameClient.HaveIncomingMessage())
			{
				NetworkMessage msg = gameClient.GetLatestMessage();

				if (msg.Packet.Type == PacketType.SChatMessage)
					OnChatMessage(msg);
				if (msg.Packet.Type == PacketType.PlayerState)
					OnPlayerState(msg);
				if (msg.Packet.Type == PacketType.SDownloadChunk)
					DownloadChunk(msg);
				if (msg.Packet.Type == PacketType.PlaceTile)
					UpdateTile(msg);
				if (msg.Packet.Type == PacketType.PlaceWall)
					UpdateWall(msg);
				if (msg.Packet.Type == PacketType.SDenyJoin)
					OnServerDenyJoining(msg);
				if (msg.Packet.Type == PacketType.SAcceptJoin)
					OnServerAcceptJoining(msg);
				if (msg.Packet.Type == PacketType.SPlayerJoined)
					OnPeerJoined(msg);
				if (msg.Packet.Type == PacketType.SPlayerLeft)
					OnPeerLeft(msg);
				if (msg.Packet.Type == PacketType.SDestroyEntity)
					OnDestroyEntity(msg);
				if (msg.Packet.Type == PacketType.ServerEntityPhysicsState)
					OnEntityPosition(msg);
				if (msg.Packet.Type == PacketType.SExplosion)
					OnExplosion(msg);
				if (msg.Packet.Type == PacketType.SpawnBombEntity)
					OnBombSpawned(msg);
				if (msg.Packet.Type == PacketType.PlaceFurniture)
					OnPlaceFurniture(msg);
				if (msg.Packet.Type == PacketType.RemoveFurniture)
					OnRemoveFurniture(msg);
				if (msg.Packet.Type == PacketType.OpenDoor)
					OnPlayerOpensDoor(msg);
				if (msg.Packet.Type == PacketType.CloseDoor)
					OnPlayerClosesDoor(msg);
				if (msg.Packet.Type == PacketType.TimeOfDay)
					OnServerChangedTimeOfDay(msg);
			}
		}


		private void DrawChunkGridLines(SpriteBatch spriteBatch)
		{
			int camChunkX = (int)Math.Floor(Camera.ScreenCenterToWorldSpace.X / (Globals.ChunkSize * Globals.TileSize));
			int camChunkY = (int)Math.Floor(Camera.ScreenCenterToWorldSpace.Y / (Globals.ChunkSize * Globals.TileSize));

			int rendr = 4;

			for (int x = -rendr; x <= rendr; x++)
			{
				spriteBatch.Line(Color.White, new Vector2(
					(camChunkX + x) * (Globals.ChunkSize * Globals.TileSize),
					(camChunkY - rendr) * (Globals.ChunkSize * Globals.TileSize)
				), new Vector2(
					(camChunkX + x) * (Globals.ChunkSize * Globals.TileSize),
					(camChunkY + rendr) * (Globals.ChunkSize * Globals.TileSize)
				), 0.5f);
			}

			for (int y = -rendr; y <= rendr; y++)
			{
				spriteBatch.Line(Color.White, new Vector2(
					(camChunkX - rendr) * (Globals.ChunkSize * Globals.TileSize),
					(camChunkY + y) * (Globals.ChunkSize * Globals.TileSize)
				), new Vector2(
					(camChunkX + rendr) * (Globals.ChunkSize * Globals.TileSize),
					(camChunkY + y) * (Globals.ChunkSize * Globals.TileSize)
				), 0.5f);
			}

			for (int x = -rendr; x <= rendr; x++)
			{
				for (int y = -rendr; y <= rendr; y++)
				{
					var pos = new Vector2(camChunkX + x, camChunkY + y) * (Globals.ChunkSize * Globals.TileSize);
					spriteBatch.Print(GameFonts.Arial8, new Color(1, 1, 1, 0.5f), pos, (camChunkX + x) + ", " + (camChunkY + y));
				}
			}
		}

		
		private void DrawChunks(SpriteBatch sb)
		{
			if (drawTimer > (1/5.0f))
			{
				drawTimer = 0;
				Chunk.RefreshedThisFrame = false;
				foreach (var chunkpair in World.Chunks)
				{
					chunkpair.Value.Draw(GameTextures.TileSheet, Game.GraphicsDevice, sb);	
				}
			}
		}

		private void EntityRendering(SpriteBatch sb)
		{
			foreach (Entity entity in World.Entities)
				entity.Draw(sb);

		}

		private void DrawChunkFGTextures(SpriteBatch sb)
		{
			foreach (var chunkpair in World.Chunks)
			{
				
				Chunk chunk = chunkpair.Value;
				Vector2 pos = new Vector2(chunk.Coordinates.X * Globals.ChunkSize * Globals.TileSize, chunk.Coordinates.Y * Globals.ChunkSize * Globals.TileSize);
				if (chunk.ForegroundRenderBuffer != null)
					sb.Draw(chunk.ForegroundRenderBuffer, pos, Color.White);

			}
		}
		private void DrawChunkBGTextures(SpriteBatch sb)
		{
			foreach (var chunkpair in World.Chunks)
			{

				Chunk chunk = chunkpair.Value;
				Vector2 pos = new Vector2(chunk.Coordinates.X * Globals.ChunkSize * Globals.TileSize, chunk.Coordinates.Y * Globals.ChunkSize * Globals.TileSize);

				if (chunk.BackgroundRenderBuffer != null)
					sb.Draw(chunk.BackgroundRenderBuffer, pos, Color.White);
			}
		}
		private void DrawDebugInfo(SpriteBatch sb)
		{
			sb.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp);
			MouseState mouse = Mouse.GetState();

			var mp = Camera.ScreenToWorldCoordinates(mouse.Position.ToVector2());

			mp /= 8;
			mp.Floor();
			var tileCoords = mp;
			mp *= 8;


			var tileat = World.GetTile((int)tileCoords.X, (int)tileCoords.Y);
			var wallat = World.GetWall((int)tileCoords.X, (int)tileCoords.Y);


			if (myPlayer != null)
			{
				sb.Print(Color.White, new Vector2(2, 12),
					String.Format("pos {0} {1} vel {2} {3}",
						Math.Floor(myPlayer.Position.X / Globals.TileSize),
						Math.Floor(myPlayer.Position.Y / Globals.TileSize),
						Math.Round(myPlayer.Velocity.X, 2),
						Math.Round(myPlayer.Velocity.Y, 2)
					)
				);
				sb.Print(Color.White, new Vector2(2, 24),
					String.Format("pin {0}, pout {1} myid {2}",
						gameClient.ReceivedCount,
						gameClient.SentCount,
						myPlayer?.EntityNetworkID
					)
				);
				sb.Print(Color.White, new Vector2(2, 36),
					String.Format("tid {0}, state {1} tdmg {2} wid {3} wdmg {4} light {5}",
						tileat.ID,
						tileat.TileState,
						tileat.Damage,
						wallat.ID,
						wallat.Damage,
						World.GetLight((int)tileCoords.X, (int)tileCoords.Y).ToString()
					)
				);
				sb.Print(Color.White, new Vector2(2, 48), String.Format("entities {0} furn {1}", World.Entities.Count, World.Furniture.Count) );

			}
			sb.End();
		}


		private void DrawSkyColor(SpriteBatch sb)
		{
			for (int y = 0; y<10; y++)
			{


				int hourTime = (int)Math.Floor( ( (World.TimeOfDay+1)%24)  / 2);
				int bottom = hourTime*2;
				int top = (hourTime*2)  + 1;
				//float diff = World.TimeOfDay % 1;
				var thisSection = Color.Lerp(World.SkyColors[bottom], World.SkyColors[top], y/10.0f);

				int prevhourTime = (int)Math.Floor((World.TimeOfDay % 24) / 2);
				int prevbottom = prevhourTime * 2;
				int prevtop = (prevhourTime * 2) + 1;
				//float diff = World.TimeOfDay % 1;
				var prevSection = Color.Lerp(World.SkyColors[prevbottom], World.SkyColors[prevtop], y / 10.0f);

				var finalColor = Color.Lerp(prevSection, thisSection, (World.TimeOfDay % 2.0f) / 2.0f);
				float sliceHeight = Camera._screenSize.Y / 10.0f;
				sb.Rect(finalColor, new Vector2(0,(sliceHeight*y)), new Vector2(Camera._screenSize.X, sliceHeight + 1));
			}
			
		}

		private SpriteBatch sbReference;
		public void Draw(SpriteBatch sb)
		{
			if (sbReference == null)
				sbReference = sb;


			DrawChunks(sb);

			Game.GraphicsDevice.Clear(World.SkyColor);
			sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp);
			DrawSkyColor(sb);
			sb.End();
			sb.Begin(SpriteSortMode.Immediate, BlendState.AlphaBlend, SamplerState.PointClamp, null, null, null, Camera.View);
			
			DrawChunkBGTextures(sb);
			DrawChunkFGTextures(sb);
			foreach(var furn in World.Furniture)
			{
				furn.Draw(GameTextures.TileSheet, sb);
			}
			EntityRendering(sb);
			
			World.ParticleSystem.Draw(sb);

			MouseState mouse = Mouse.GetState();

			var mp = Camera.ScreenToWorldCoordinates(mouse.Position.ToVector2());

			mp /= 8;
			mp.Floor();
			var tileCoords = mp;
			mp *= 8;


			if (mouse.LeftButton == ButtonState.Pressed)
			{
				sb.Rect(Color.Green, mp, new Vector2(8, 8));
			} else
			{
				sb.Rect(new Color(1,1,1,0.5f), mp, new Vector2(8, 8));
			}

			sb.End();
			if (ShowChunkBoundaries)
			{
				sb.Begin(SpriteSortMode.Deferred, null, SamplerState.PointClamp, null, null, null, Camera.View);
				DrawChunkGridLines(sb);
				sb.End();
			}

			Hotbar.Draw(sb);

			DrawDebugInfo(sb);
			Chat.Draw(sb);
		}

		public void Load()
		{
			Hotbar = new Hotbar();
			ServerConfig localServerConf = new ServerConfig
			{
				Port = 40269,
				World = "LocalWorld",
				MaxPlayers = 2,
				ServerName = "LocalServer",
				TickRate = 1,
			};

#if SERVER
			gameServer = new GameServer(localServerConf);
			gameServer.Output = Game.Console;

			gameServer.Start();
			gameServer.ClientRun();
#endif
			//	gameClient = new NetworkClient("72.243.56.7", 40269);
			gameClient = new NetworkClient(ConnectAddress);

			gameClient.Output = Game.Console;
			gameClient.Start();
			gameClient.SendPacket(new RequestJoinPacket(NetworkUsername));
		}

		public void Unload() {}
		MouseState previous = Mouse.GetState();
		private void HotbarUpdate(GameTime gt)
		{
			MouseState mouse = Mouse.GetState();

			// Just Pressed
			if (mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Released)
				Hotbar.ItemSet[Hotbar.Index].OnClientLMBDown(myPlayer, this);

			// Just Released
			if (mouse.LeftButton == ButtonState.Released && previous.LeftButton == ButtonState.Pressed)
				Hotbar.ItemSet[Hotbar.Index].OnClientLMBUp(myPlayer, this);

			// Continued Left Mouse Down
			if (mouse.LeftButton == ButtonState.Pressed && previous.LeftButton == ButtonState.Pressed)
				Hotbar.ItemSet[Hotbar.Index].OnClientLMBHeld(myPlayer, this);

			// Object interaction
			if (mouse.RightButton == ButtonState.Pressed && previous.RightButton == ButtonState.Released)
			{
				var mp = Camera.ScreenToWorldCoordinates(mouse.Position.ToVector2());
				Point pos = new Point(
					(int)Math.Floor(mp.X / Globals.TileSize),
					(int)Math.Floor(mp.Y / Globals.TileSize)
				);
				var maybeFurniture = World.GetFurnitureAt(pos.X, pos.Y);

				if (maybeFurniture!=null)
				{
					//if (maybeFurniture is WoodenDoor door)
						maybeFurniture.OnPlayerInteracts(myPlayer, World, this);
				}

			}
			previous = mouse;

		}


		private void ReplicatePlayerState()
		{
			//Debug.WriteLine("Replicating");
			if (myPlayer != null)
			{
				gameClient?.SendPacket(
					new EntityPositionPacket(myPlayer.EntityNetworkID, myPlayer.Position.X, myPlayer.Position.Y,
						myPlayer.Velocity.X, myPlayer.Velocity.Y, myPlayer.NextPosition.X, myPlayer.NextPosition.Y)
				);

				gameClient?.SendPacket(new PlayerStatePacket(myPlayer.Facing, myPlayer.OnGround, myPlayer.Walking));
			}
		}


		int scroll = 0;
		private void UpdateCamera(GameTime gt)
		{
			MouseState mouse = Mouse.GetState();

			if (Keyboard.GetState().IsKeyDown(Keys.LeftShift))
			{
				scroll += mouse.ScrollWheelValue;
			}

			float Senitivity = (float)0.05;
			float ZoomFactor = ((mouse.ScrollWheelValue - IntitalScrollValue) * (Senitivity / 120)) + 2;

			Vector2 MouseCameraMovement = ((mouse.Position.ToVector2() / Camera._screenSize) - new Vector2(0.5f, 0.5f)) * 5.5f;

			

			Camera.Zoom = ZoomFactor;

			if (myPlayer != null)
			{
				if (((Camera.Position) - myPlayer.Position).Length() < 500f)
				{
					float speed = (float)(gt.ElapsedGameTime.TotalSeconds * 10.0);
					Camera.Position = Camera.Position.Lerp(myPlayer.Position + MouseCameraMovement, speed);
				}
				else
				{
					Camera.Position = myPlayer.Position;
				}
			}
		}


		float drawTimer = 0;
		KeyboardState previousKB = Keyboard.GetState();
		public void Update(GameTime gt)
		{
			KeyboardState keyboard = Keyboard.GetState();

			if (keyboard.IsKeyDown(Keys.F3) && !previousKB.IsKeyDown(Keys.F3))
				ShowChunkBoundaries = !ShowChunkBoundaries;


			previousKB = keyboard;


			drawTimer += (float)gt.ElapsedGameTime.TotalSeconds;
			
			playerStateReplicationTask.Update(gt);
			chunkUnloadingTask.Update(gt);
			chunkLoadingTask.Update(gt);

			Chat.Update(gt);
			Hotbar.Update(gt);
			World.Update(gt);

			
			HotbarUpdate(gt);
			UpdateCamera(gt);

			//gameServer?.Update(gt);
			
			//ChunkUnloadingCheckUpdate(gt);
			ReadIncomingPackets();


			if (Keyboard.GetState().IsKeyDown(Keys.Escape))
				Game.CurrentGameContext = Game.HomePageContext;
		}
	}
}
