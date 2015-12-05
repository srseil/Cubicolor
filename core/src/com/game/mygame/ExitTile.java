package com.game.mygame;

import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

import java.util.EnumSet;

public class ExitTile extends Tile {

	private int height;
	private int defaultHeight;
	private EnumSet<TileAttributes.TColor> requirements;
	private ExitTileModel observer;

	public ExitTile(EnumSet<TileAttributes.TColor> requirements) {
		super();
		this.requirements = requirements;
		this.defaultHeight = requirements.size();
		this.height = defaultHeight;
	}

	public void lower() {
		height -= 1;
	}

	public void removeRequirement(TileAttributes.TColor req) {
		requirements.remove(req);
		height--;
		notifyObserver();
	}

	public void reset(EnumSet<TileAttributes.TColor> requirements) {
		this.requirements = requirements;
		height = defaultHeight;
		notifyObserver();
	}

	@Override
	public void draw(float x, float y, ShapeRenderer renderer) {
		renderer.setColor(0.1f, 0.1f, 0.1f, 1.0f);
		renderer.rect(x, y, 50, 50);

		float i = 1.0f;
		for (TileAttributes.TColor color : requirements) {
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

	public void addObserver(ExitTileModel model) {
		observer = model;
	}

	@Override
	public void notifyObserver() {
		observer.updateState();
	}

	public EnumSet<TileAttributes.TColor> getRequirements() {
		return requirements;
	}

	public int getHeight() {
		return height;
	}

}
