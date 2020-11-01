﻿using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;
using System.Text;

namespace CaveGame.Core.Tiles
{
	public class BaseGrass : Tile, ISoil
	{
		public BaseGrass(TDef def) : base(def) { }

		public static Rectangle Patch = new Rectangle(8 * Globals.TileSize, 6 * Globals.TileSize, Globals.TileSize, Globals.TileSize);

		public override void Draw(Texture2D tilesheet, SpriteBatch sb, int x, int y, Light3 color)
		{
#if EDITOR
			sb.Draw(tilesheet, new Vector2(x * Globals.TileSize, y * Globals.TileSize), Quad, Color);
			return;
#endif

			sb.Draw(tilesheet, new Vector2(x * Globals.TileSize, y * Globals.TileSize), TileMap.Soil, color.MultiplyAgainst(Color.SaddleBrown));
			var corner = new Rectangle(9 * Globals.TileSize, 6 * Globals.TileSize, Globals.TileSize, Globals.TileSize);
			Vector2 position = new Vector2(x * Globals.TileSize, y * Globals.TileSize) + new Vector2(4, 4);

			if (TileState.Get(0)) // cornerd
				sb.Draw(tilesheet, position, corner, color.MultiplyAgainst(Color), MathHelper.ToRadians(270), new Vector2(4, 4), 1, SpriteEffects.None, 1);
			if (TileState.Get(1)) // cornerc
				sb.Draw(tilesheet, position, corner, color.MultiplyAgainst(Color), MathHelper.ToRadians(180), new Vector2(4, 4), 1, SpriteEffects.None, 1);
			if (TileState.Get(2)) // cornerb
				sb.Draw(tilesheet, position, corner, color.MultiplyAgainst(Color), MathHelper.ToRadians(90), new Vector2(4, 4), 1, SpriteEffects.None, 1);
			if (TileState.Get(3)) // cornera
				sb.Draw(tilesheet, position, corner, color.MultiplyAgainst(Color), 0, new Vector2(4, 4), 1, SpriteEffects.None, 1);
			if (TileState.Get(4)) // planeright
				sb.Draw(tilesheet, position, Patch, color.MultiplyAgainst(Color), MathHelper.ToRadians(90), new Vector2(4, 4), 1, SpriteEffects.None, 1);
			if (TileState.Get(5)) // planebottom
				sb.Draw(tilesheet, position, Patch, color.MultiplyAgainst(Color), MathHelper.ToRadians(0), new Vector2(4, 4), 1, SpriteEffects.FlipVertically, 1);
			if (TileState.Get(6)) // planeleft
				sb.Draw(tilesheet, position, Patch, color.MultiplyAgainst(Color), MathHelper.ToRadians(270), new Vector2(4, 4), 1, SpriteEffects.None, 1);
			if (TileState.Get(7)) // planetop
				sb.Draw(tilesheet, position, Patch, color.MultiplyAgainst(Color), MathHelper.ToRadians(0), new Vector2(4, 4), 1, SpriteEffects.None, 1);

		}

		private bool CanBreathe(IGameWorld world, int x, int y)
		{
			var above = world.GetTile(x, y - 1);
			var below = world.GetTile(x, y + 1);
			var left = world.GetTile(x - 1, y);
			var right = world.GetTile(x + 1, y);
			var tleft = world.GetTile(x - 1, y - 1);
			var tright = world.GetTile(x + 1, y - 1);
			var bleft = world.GetTile(x - 1, y + 1);
			var bright = world.GetTile(x + 1, y + 1);

			return (above is INonSolid || below is INonSolid || left is INonSolid || right is INonSolid || tleft is INonSolid || tright is INonSolid || bleft is INonSolid || bright is INonSolid);
		}

		private bool IsMatch<T>(IGameWorld w, int x, int y)
		{
			if (w.GetTile(x, y) is T)
			{
				return true;
			}
			return false;
		}


		public void Spread<TGrass, TDecay, TAbove, TBelow>(IGameWorld world, int x, int y) where TGrass : Tile, new() where TAbove : Tile, new() where TBelow : Tile, new() where TDecay : Tile, new()
		{
			var above = world.GetTile(x, y - 1);
			var below = world.GetTile(x, y + 1);
			var left = world.GetTile(x - 1, y);
			var right = world.GetTile(x + 1, y);


			if (!CanBreathe(world, x, y))
			{
				// suffocate

				world.SetTile(x, y, new TDecay());
				return;
			}

			if (above is Air)
			{
				world.SetTile(x, y - 1, new TAbove());
			}

			if (below is Air)
			{
				world.SetTile(x, y + 1, new TBelow());
			}


			if (below is TDecay && CanBreathe(world, x, y + 1))
			{
				world.SetTile(x, y + 1, new TGrass());
				//return;
			}

			if (left is TDecay && CanBreathe(world, x - 1, y))
			{
				world.SetTile(x - 1, y, new TGrass());
				//return;
			}


			if (right is TDecay && CanBreathe(world, x + 1, y))
			{
				world.SetTile(x + 1, y, new TGrass());
				//return;
			}


			if (above is TDecay && CanBreathe(world, x, y - 1))
			{
				world.SetTile(x, y - 1, new TGrass());
				//return;
			}
		}

		public void LocalTileUpdate<T>(IGameWorld world, int x, int y)
		{
			bool planetop = IsEmpty(world, x, y - 1);
			bool planebottom = IsEmpty(world, x, y + 1);
			bool planeleft = IsEmpty(world, x - 1, y);
			bool planeright = IsEmpty(world, x + 1, y);
			bool air_tl = IsEmpty(world, x - 1, y - 1);
			bool air_bl = IsEmpty(world, x - 1, y + 1);
			bool air_tr = IsEmpty(world, x + 1, y - 1);
			bool air_br = IsEmpty(world, x + 1, y + 1);
			bool gabove = IsMatch<T>(world, x, y - 1);
			bool gbelow = IsMatch<T>(world, x, y + 1);
			bool gleft = IsMatch<T>(world, x - 1, y);
			bool gright = IsMatch<T>(world, x + 1, y);
			bool cornera = (gleft && gabove && air_tl);
			bool cornerb = (gright && gabove && air_tr);
			bool cornerc = (gright && gbelow && air_br);
			bool cornerd = (gleft && gbelow && air_bl);
			byte newNumber = TileState;
			newNumber.Set(7, planetop);
			newNumber.Set(6, planeleft);
			newNumber.Set(5, planebottom);
			newNumber.Set(4, planeright);
			newNumber.Set(3, cornera);
			newNumber.Set(2, cornerb);
			newNumber.Set(1, cornerc);
			newNumber.Set(0, cornerd);


			if (TileState != newNumber)
			{
				TileState = newNumber;
				world.DoUpdatePropogation(x, y);
			}
		}
	}
}
