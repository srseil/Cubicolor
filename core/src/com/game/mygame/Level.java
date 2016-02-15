package com.game.mygame;

import java.util.ArrayList;
import java.util.EnumSet;

public class Level {

	// Properties of level file.
	private Difficulty difficulty;
	private int number;
	private int optimal;
	private int startRow, startColumn;
	//private EnumSet<TileColor> exitRequirements;
	private ArrayList<TileColor> exitRequirements;

	public int rows, columns;
	public Tile[][] matrix;
	private ExitTile exit;

	public Level(Difficulty difficulty, int number, int optimal,
				 int startRow, int startColumn,
				 //EnumSet<TileColor> exitRequirements,
				 ArrayList<TileColor> exitRequirements,
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

		// Look for exit tile in matrix and assign it to reference.
		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < columns; j++) {
				if (matrix[i][j] instanceof ExitTile) {
					exit = (ExitTile) matrix[i][j];
					return;
				}
			}
		}
	}

	/*
	 * Reset the level to its default state.
	 */
	public void reset() {
		// Reset the exit tile to its default requirements.
		ArrayList<TileColor> reqs = new ArrayList<>(exitRequirements);
		//exit.reset(exitRequirements.clone());
		exit.reset(reqs);

		// Reset individual tiles in matrix.
		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < columns; j++) {
				if (matrix[i][j] instanceof EmptyTile
						|| matrix[i][j] instanceof ExitTile) {
					// Tile is empty or exit; set to dead.
					matrix[i][j].setDead(true);
				} else if (matrix[i][j].isDead()) {
					// Tile is dead but should not be, set to alive.
					matrix[i][j].setDead(false);
				}

				// Reset key and lock tiles.
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

	/*
	 * Fulfill a requirement of the exit with the specified color.
	 */
	public void fulfillRequirement(TileColor color) {
		exit.removeRequirement(color);
	}

	/*
	 * Check if all requirements of the exit are met.
	 */
	public boolean requirementsMet() {
		return (exit.getRequirements().size() == 0);
	}

	/*
	 * Return the number of requirements to be fulfilled for the level.
	 */
	public int getRequirementsNumber() {
		return exitRequirements.size();
	}

	//public EnumSet<TileColor> getExitRequirements() {
	public ArrayList<TileColor> getExitRequirements() {
		return exitRequirements;
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

	public Tile[][] getMatrix() {
		return matrix;
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

}

