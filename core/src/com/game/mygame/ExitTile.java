package com.game.mygame;

import java.util.EnumSet;

public class ExitTile extends Tile {

	private int height;
	private int defaultHeight;
	private EnumSet<TileColor> requirements;

	public ExitTile(EnumSet<TileColor> requirements) {
		super();
		this.requirements = requirements;
		this.defaultHeight = requirements.size();
		this.height = defaultHeight;
	}

	public void removeRequirement(TileColor req) {
		requirements.remove(req);
		height--;
		notifyObserver();
	}

	public void reset(EnumSet<TileColor> requirements) {
		this.requirements = requirements;
		height = defaultHeight;
		// no notify.
	}

	public EnumSet<TileColor> getRequirements() {
		return requirements;
	}

	public int getHeight() {
		return height;
	}

}
