package com.game.mygame;

public class EmptyTile extends Tile {

    public EmptyTile() {
        dead = true;
    }

    @Override
    public void setDead(boolean dead) {}

}

