package com.game.mygame;

import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

/**
 * Created by stefan on 20.11.15.
 */
public class KeyTile extends Tile {

    private TileColor color;
    private boolean key;
    //private Circle paint;

    public KeyTile(TileColor color) {
        super();
        this.color = color;
        key = true;
    }

    public void takeKey() {
        key = false;
        // Change appearance.
    }

    public void reset() {
        key = true;
    }

    @Override
    public void draw(float x, float y, ShapeRenderer renderer) {
        renderer.setColor(1.0f, 1.0f, 1.0f, 1.0f);
        renderer.rect(x, y, 50, 50);

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
        renderer.circle(x + 25.0f, y + 25.0f, 12.5f);
    }

    public TileColor getColor() {
        return color;
    }

    public boolean hasKey() {
        return key;
    }

}
