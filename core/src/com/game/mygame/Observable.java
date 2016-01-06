package com.game.mygame;

public interface Observable {

	void addObserver(Observer observer);

	void notifyObserver(Object... args);

}

