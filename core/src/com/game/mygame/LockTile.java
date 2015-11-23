package com.game.mygame;

import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

public class LockTile extends Tile {

    private TileColor color;
    //private Circle paint;

    public LockTile(TileColor color) {
        super();
        this.color = color;
    }

    public void unlock() {
        // Trigger some animation.
    }

    public void reset() {
    }

    @Override
    //public void draw(float x, float y, SpriteBatch batch) {
    public void draw(float x, float y, ShapeRenderer renderer) {
        switch (color) {
            case RED:
                renderer.setColor(1.0f, 0.1f, 0.1f, 1);
                break;
            case GREEN:
                renderer.setColor(0.1f, 1.0f, 0.1f, 1);
                break;
            case BLUE:
                renderer.setColor(0.1f, 0.1f, 1.0f, 1);
                break;
            case YELLOW:
                renderer.setColor(1.0f, 1.0f, 0.1f, 1);
                break;
            default:
                renderer.setColor(0.0f, 0.0f, 0.0f, 1);
        }
        renderer.rect(x, y, 50, 50);
    }

    public TileColor getColor() {
        return color;
    }

}
