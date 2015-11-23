package com.game.mygame;

import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

import java.util.EnumSet;

public class ExitTile extends Tile {

    private float height;
    private float defaultHeight;
    private EnumSet<TileColor> requirements;

    public ExitTile(EnumSet<TileColor> requirements) {
        super();
        this.requirements = requirements;
        this.defaultHeight = requirements.size();
    }

    public void lower() {
        height -= 1.0f;
    }

    public void removeRequirement(TileColor req) {
        requirements.remove(req);
    }

    public void reset(EnumSet<TileColor> requirements) {
        this.requirements = requirements;
    }

    @Override
    public void draw(float x, float y, ShapeRenderer renderer) {
        renderer.setColor(0.1f, 0.1f, 0.1f, 1.0f);
        renderer.rect(x, y, 50, 50);

        float i = 1.0f;
        for (TileColor color : requirements) {
            switch (color) {
                case RED:
                    renderer.setColor(1, 0, 0, 1);
                    break;
                case GREEN:
                    renderer.setColor(0, 1, 0, 1);
                    break;
                case BLUE:
                    renderer.setColor(0, 0, 1, 1);
                    break;
                case YELLOW:
                    renderer.setColor(1, 1, 0, 1);
                    break;
                default:
                    renderer.setColor(0, 0, 0, 1);
            }
            renderer.circle(x + 12.5f*i, y + 25.0f, 12.5f);
            i++;
        }
    }

    public EnumSet<TileColor> getRequirements() {
        return requirements;
    }

}
