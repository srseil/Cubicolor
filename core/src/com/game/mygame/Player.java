package com.game.mygame;

public class Player implements Observable {

	private Level level;
	private int x, y;
	private int steps;
	private TileColor key;
	private GameScreen gameScreen;
	private boolean completed;
	private Observer observer;

	public Player(Level level, GameScreen gameScreen) {
		this.level = level;
		this.gameScreen = gameScreen;
		x = level.getStartColumn();
		y = level.getStartRow();
		steps = 0;
		key = TileColor.NONE;
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

	@Override
	public void addObserver(Observer observer) {
		this.observer = observer;
	}

	@Override
	public void notifyObserver() {
		observer.updateState();
	}

	private void interact() {
		level.getMatrix()[y][x].interact(this);
		notifyObserver();
	}

	public void reset() {
		x = level.getStartColumn();
		y = level.getStartRow();
		steps = 0;
		key = TileColor.NONE;
		completed = false;
	}

	public void takeKey(TileColor color) {
		key = color;
		notifyObserver();
	}

	public void removeKey() {
		key = TileColor.NONE;
		notifyObserver();
	}

	public void fulfillRequirement(TileColor color) {
		level.fulfillRequirement(color);
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

	public TileColor getKey() {
		return key;
	}

}
