package com.game.mygame;

import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

public class Tile {

	protected boolean dying;
	protected boolean dead;

	public Tile() {
		dying = false;
		dead = false;
	}

	//public void draw(SpriteBatch batch) {
	public void draw(float x, float y, ShapeRenderer renderer) {
		renderer.setColor(1, 1, 1, 1);
		renderer.rect(x, y, 50, 50);
	}

	public boolean isDying() {
		return dying;
	}

	public void setDying(boolean dying) {
		this.dying = dying;
	}

	public boolean isDead() {
		return dead;
	}

	public void setDead(boolean dead) {
		this.dead = dead;
	}

}
