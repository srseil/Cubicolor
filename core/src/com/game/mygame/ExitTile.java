package com.game.mygame;

import java.util.EnumSet;

public class ExitTile extends Tile {

	private int height;
	private int defaultHeight;
	private EnumSet<TileAttributes.TColor> requirements;

	public ExitTile(EnumSet<TileAttributes.TColor> requirements) {
		super();
		this.requirements = requirements;
		this.defaultHeight = requirements.size();
		this.height = defaultHeight;
	}

	public void removeRequirement(TileAttributes.TColor req) {
		requirements.remove(req);
		height--;
		notifyObserver();
	}

	public void reset(EnumSet<TileAttributes.TColor> requirements) {
		this.requirements = requirements;
		height = defaultHeight;
		// no notify.
	}

	public EnumSet<TileAttributes.TColor> getRequirements() {
		return requirements;
	}

	public int getHeight() {
		return height;
	}

}
