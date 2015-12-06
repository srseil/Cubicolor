package com.game.mygame;

import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

public class Player {

	private Level level;
	private int x, y;
	private int steps;
	private TileAttributes.TColor key;
	private GameScreen gameScreen;
	private boolean completed;
	private AnimatedModel observer;

	public Player(Level level, GameScreen gameScreen) {
		this.level = level;
		this.gameScreen = gameScreen;
		x = level.getStartColumn();
		y = level.getStartRow();
		steps = 0;
		key = TileAttributes.TColor.NONE;
		completed = false;
	}

	public boolean move(int x, int y) {
		int newX = this.x + x;
		int newY = this.y + y;
		if (newX >= level.getColumns() || newX < 0
				|| newY >= level.getRows() || newY < 0) {
			notifyObserver();
			return false;
		}

		Tile toTile = level.getMatrix()[newY][newX];
		if (!toTile.isDead() &&
				!(toTile instanceof ExitTile && !level.requirementsMet())) {
			//level.getMatrix()[this.y][this.x].setDying(true);
			level.getMatrix()[this.y][this.x].setDead(true);
			this.x = newX;
			this.y = newY;
			steps++;
			notifyObserver();
			interact();
			return true;
		} else {
			notifyObserver();
			return false;
		}
	}

	private void interact() {
		Tile tile = level.getMatrix()[y][x];
		if (tile instanceof KeyTile) {
			KeyTile keyTile = (KeyTile) tile;
			addKey(keyTile.getColor());
			keyTile.takeKey();
		} else if (tile instanceof LockTile) {
			LockTile lockTile = (LockTile) tile;
			if (key == lockTile.getColor()) {
				lockTile.unlock();
				level.fulfillRequirement(key);
				removeKey();
			}
		} else if (tile instanceof ExitTile) {
			/*
			if (steps <= level.getOptimalSteps())
				gameScreen.completeLevel(true);
			else
				gameScreen.completeLevel(false);
			completed = true;
			*/
		}
		notifyObserver();
	}

	public void reset() {
		x = level.getStartColumn();
		y = level.getStartRow();
		steps = 0;
		key = TileAttributes.TColor.NONE;
		completed = false;
	}

	public void draw(float baseX, float baseY, ShapeRenderer renderer) {
		if (key != TileAttributes.TColor.NONE) {
			renderer.setColor(TileAttributes.getGDXColor(key));
			renderer.rect(baseX + 50*x, baseY + 50*y, 50, 50);
			renderer.end();
			renderer.begin(ShapeRenderer.ShapeType.Line);
			renderer.setColor(0, 0, 0, 1);
			renderer.rect(baseX + 50*x, baseY + 50*y, 50, 50);
			renderer.end();
			renderer.begin(ShapeRenderer.ShapeType.Filled);
		} else {
			renderer.setColor(0, 0, 0, 1);
			renderer.rect(baseX + 50*x, baseY + 50*y, 50, 50);
		}
	}

	private void addKey(TileAttributes.TColor color) {
		key = color;
	}

	private void removeKey() {
		key = TileAttributes.TColor.NONE;
		// Trigger animation.
	}

	/*
	private Color getColor(TileColor tileColor) {
		switch (tileColor) {
			case RED:
				return new Color(1, 0, 0, 1);
			case GREEN:
				return new Color(0, 1, 0, 1);
			case BLUE:
				return new Color(0, 0, 1, 1);
			case YELLOW:
				return new Color(1, 1, 0, 1);
			default:
				return new Color(0, 0, 0, 1);
		}
	}
	*/

	public void addObserver(AnimatedModel observer) {
		this.observer = observer;
	}

	public void notifyObserver() {
		observer.updateState();
	}

	public int getSteps() {
		return steps;
	}

	public boolean hasCompleted() {
		return completed;
	}

	public float getX() {
		return x;
	}

	public float getY() {
		return y;
	}

	public TileAttributes.TColor getKey() {
		return key;
	}

}
