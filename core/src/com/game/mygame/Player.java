package com.game.mygame;

import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

public class Player {

    private Level level;
    private int x, y;
    private int steps;
    private TileColor key;
	private GameScreen gameScreen;
	private boolean completed;

    public Player(Level level, GameScreen gameScreen) {
        this.level = level;
		this.gameScreen = gameScreen;
        x = level.getStartColumn();
        y = level.getStartRow();
        steps = 0;
        key = null;
		completed = false;
    }

    public void move(int x, int y) {
        int newX = this.x + x;
        int newY = this.y + y;
        if (newX >= level.getColumns() || newX < 0
                || newY >= level.getRows() || newY < 0) {
            return;
        }

        Tile toTile = level.getMatrix()[newY][newX];
        if (!toTile.isDead()) {
            if (toTile instanceof ExitTile && !level.requirementsMet())
                return;
            // Trigger animation.
            level.getMatrix()[this.y][this.x].setDead(true);
            this.x = newX;
            this.y = newY;
            steps++;

            interact();
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
            gameScreen.completeLevel();
			completed = true;
        }
    }

    public void reset() {
        x = level.getStartColumn();
        y = level.getStartRow();
        steps = 0;
        key = null;
		completed = false;
    }

    public void draw(float baseX, float baseY, ShapeRenderer renderer) {
        if (key != null) {
            renderer.setColor(getColor(key));
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

    private void addKey(TileColor color) {
        key = color;
    }

    private void removeKey() {
        key = null;
        // Trigger animation.
    }

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

    public int getSteps() {
        return steps;
    }

public boolean hasCompleted() {
		return completed;
	}

}
