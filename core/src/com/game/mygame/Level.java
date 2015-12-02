package com.game.mygame;

import java.util.EnumSet;

public class Level {

	// Properties of level file.
	private Difficulty difficulty;
	private int number;
	private int optimal;
	private int startRow, startColumn;
	private EnumSet<TileAttributes.TColor> exitRequirements;

	public int rows, columns;
	public Tile[][] matrix;
	private ExitTile exit;

	public Level(Difficulty difficulty, int number, int optimal,
				 int startRow, int startColumn,
				 EnumSet<TileAttributes.TColor> exitRequirements,
				 Tile[][] matrix) {
		this.difficulty = difficulty;
		this.number = number;
		this.optimal = optimal;
		this.startRow = startRow;
		this.startColumn = startColumn;
		this.exitRequirements = exitRequirements;
		this.matrix = matrix;

		rows = matrix.length;
		columns = matrix[0].length;

		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < columns; j++) {
				if (matrix[i][j] instanceof ExitTile) {
					exit = (ExitTile) matrix[i][j];
				}
			}
		}

	}

	public void reset() {
		EnumSet<TileAttributes.TColor> reqs = exitRequirements.clone();
		exit.reset(reqs);

		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < columns; j++) {
				/*
				if (matrix[i][j].isDead()) {
					matrix[i][j].setDead(false);
					matrix[i][j].notifyObservers();
				}
				if (matrix[i][j].isDying()) {
					matrix[i][j].setDying(false);
					matrix[i][j].setDead(true);
				}
				*/

				if (!(matrix[i][j] instanceof EmptyTile) &&
						matrix[i][j].isDead()) {
					matrix[i][j].setDead(false);
					//matrix[i][j].setReviving(true);
				}

				if (matrix[i][j] instanceof KeyTile) {
					KeyTile keyTile = (KeyTile) matrix[i][j];
					keyTile.reset();
				} else if (matrix[i][j] instanceof LockTile) {
					LockTile lockTile = (LockTile) matrix[i][j];
					lockTile.reset();
				}
			}
		}
	}

	public void fulfillRequirement(TileAttributes.TColor color) {
		exit.removeRequirement(color);
	}

	public boolean requirementsMet() {
		if (exit.getRequirements().size() == 0) {
			return true;
		} else {
			return false;
		}
	}

	public Difficulty getDifficulty() {
		return difficulty;
	}

	public int getNumber() {
		return number;
	}

	public int getOptimalSteps() {
		return optimal;
	}

	public int getRows() {
		return rows;
	}

	public int getColumns() {
		return columns;
	}

	public int getStartRow() {
		return startRow;
	}

	public int getStartColumn() {
		return startColumn;
	}

	public Tile[][] getMatrix() {
		return matrix;
	}

}
