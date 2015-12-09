package com.game.mygame;

public class Tile {

	protected AnimatedModel observer;
	protected boolean dead;

	public Tile() {
		dead = false;
	}

	public void interact(Player player) {}

	public void addObserver(AnimatedModel observer) {
		this.observer = observer;
	}

	public void notifyObserver() {
		observer.updateState();
	}

	public boolean isDead() {
		return dead;
	}

	public void setDead(boolean dead) {
		this.dead = dead;
		notifyObserver();
	}

}
