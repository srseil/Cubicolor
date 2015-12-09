package com.game.mygame;

public class Tile implements Observable {

	protected Observer observer;
	protected boolean dead;

	public Tile() {
		dead = false;
	}

	@Override
	public void addObserver(Observer observer) {
		this.observer = observer;
	}

	@Override
	public void notifyObserver() {
		observer.updateState();
	}

	public void interact(Player player) {}

	public boolean isDead() {
		return dead;
	}

	public void setDead(boolean dead) {
		this.dead = dead;
		notifyObserver();
	}

}
