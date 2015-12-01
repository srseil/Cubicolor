package com.game.mygame;

import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

public class Tile {

	protected boolean dead;
	protected boolean dying;
	protected boolean reviving;

	public Tile() {
		dead = false;
		dying = false;
		reviving = false;
	}

	//public void draw(SpriteBatch batch) {
	public void draw(float x, float y, ShapeRenderer renderer) {
		renderer.setColor(1, 1, 1, 1);
		renderer.rect(x, y, 50, 50);
	}

	public boolean isDead() {
		return dead;
	}

	public void setDead(boolean dead) {
		this.dead = dead;
	}

	public boolean isDying() {
		return dying;
	}

	public void setDying(boolean dying) {
		this.dying = dying;
	}

	public boolean isReviving() {
		return reviving;
	}

	public void setReviving(boolean reviving) {
		this.reviving = reviving;
	}

}
