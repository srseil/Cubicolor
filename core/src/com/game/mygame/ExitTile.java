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
		dead = true;
	}

	@Override
	public void setDead(boolean dead) {
		this.dead = dead;
	}

	public void removeRequirement(TileColor req) {
		requirements.remove(req);
		/*
		if (requirements.size() == 0)
			dead = false;
		*/

		height--;
		Object[] args = {req};
		notifyObserver(args);
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
