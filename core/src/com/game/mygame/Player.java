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
		level.getMatrix()[y][x].interact(this);
		notifyObserver();
	}

	public void reset() {
		x = level.getStartColumn();
		y = level.getStartRow();
		steps = 0;
		key = TileAttributes.TColor.NONE;
		completed = false;
	}

	public void takeKey(TileAttributes.TColor color) {
		key = color;
		notifyObserver();
	}

	public void removeKey() {
		key = TileAttributes.TColor.NONE;
		notifyObserver();
	}

	public void fulfillRequirement(TileAttributes.TColor color) {
		level.fulfillRequirement(color);
	}

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

	public int getX() {
		return x;
	}

	public int getY() {
		return y;
	}

	public TileAttributes.TColor getKey() {
		return key;
	}

}
