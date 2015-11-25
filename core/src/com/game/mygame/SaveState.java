package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.files.FileHandle;
import com.badlogic.gdx.utils.GdxRuntimeException;
import com.badlogic.gdx.utils.XmlReader;

import java.io.IOException;

public class SaveState {

	private XmlReader reader;
	private SolveState levels[][];

	public SaveState() {
		reader = new XmlReader();
		levels = new SolveState[1][3];
	}

	// Loads a save state from disk.
	public void load() throws IOException {
		FileHandle handle = Gdx.files.internal("savestate.xml");
		XmlReader.Element root = reader.parse(handle);

		try {
			XmlReader.Element normal = root.getChildByName("normal");
			XmlReader.Element smart = root.getChildByName("smart");
			XmlReader.Element genius = root.getChildByName("genius");

			for (int i = 0; i < levels[0].length; i++) {
				switch (normal.get("level" + Integer.toString(i+1))) {
					case "unsolved":
						levels[0][i] = SolveState.UNSOLVED;
						break;
					case "solved":
						levels[0][i] = SolveState.SOLVED;
						break;
					case "optimal":
						levels[0][i] = SolveState.OPTIMAL;
						break;
					default:
						throw new IOException();
				}
			}
		} catch (GdxRuntimeException exception) {
			exception.printStackTrace();
			throw new IOException();
		}
	}

	// Updates the save state with the corresponding completed level.
	public void update(String difficulty, int number, boolean optimal) {
		SolveState state;
		if (optimal)
			state = SolveState.OPTIMAL;
		else
			state = SolveState.SOLVED;
		levels[0][number-1] = state;
	}

	// Resets a save state to no completed levels.
	public void reset() {

	}

	public SolveState getSolveState(String difficulty, int number) {
		return levels[0][number-1];
	}
}
