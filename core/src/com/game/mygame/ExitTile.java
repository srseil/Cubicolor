package com.game.mygame;

import java.util.ArrayList;
import java.util.EnumSet;

public class ExitTile extends Tile {

	private int height;
	private int defaultHeight;
	//private EnumSet<TileColor> requirements;
	private ArrayList<TileColor> requirements;

	//public ExitTile(EnumSet<TileColor> requirements) {
	public ExitTile(ArrayList<TileColor> requirements) {
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

	/*
	 * Fulfills a requirement with the specified color.
	 */
	public void removeRequirement(TileColor req) {
		requirements.remove(req);
		height--;
		Object[] args = {req};
		notifyObserver(args);
	}

	/*
	 * Resets the tile to default height and requirements.
	 * Does not notify observer, because resetting is also calling the models.
	 */
	//public void reset(EnumSet<TileColor> requirements) {
	public void reset(ArrayList<TileColor> requirements) {
		this.requirements = requirements;
		height = defaultHeight;
	}

	//public EnumSet<TileColor> getRequirements() {
	public ArrayList<TileColor> getRequirements() {
		return requirements;
	}

	public int getHeight() {
		return height;
	}

}

