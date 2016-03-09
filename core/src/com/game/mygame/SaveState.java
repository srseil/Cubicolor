package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.files.FileHandle;
import com.badlogic.gdx.utils.GdxRuntimeException;
import com.badlogic.gdx.utils.XmlReader;
import com.badlogic.gdx.utils.XmlWriter;

import java.io.File;
import java.io.IOException;
import java.io.PrintWriter;

public class SaveState {

	private XmlReader reader;
	private SolveState levels[][];

	public SaveState() {
		reader = new XmlReader();
		levels = new SolveState[Difficulty.values().length][16];
	}

	// Loads a save state from disk.
	public void load() throws IOException {
		String path = "savestates/savestate.xml";
		FileHandle handle = Gdx.files.internal(path);
		XmlReader.Element root = reader.parse(handle);

		try {
			XmlReader.Element diffElement = null;

			for (int i = 0; i < levels.length; i++) {
				switch (i) {
					case 0: diffElement = root.getChildByName("normal"); break;
					case 1: diffElement = root.getChildByName("smart"); break;
					case 2: diffElement = root.getChildByName("genius"); break;
				}
				for (int j = 0; j < levels[i].length; j++) {
					boolean solved = diffElement.getBoolean(
							"level" + Integer.toString((j + 1)));
					if (solved)
						levels[i][j] = SolveState.SOLVED;
					else
						levels[i][j] = SolveState.UNSOLVED;
				}
			}
		} catch (GdxRuntimeException exception) {
			exception.printStackTrace();
			throw new IOException();
		}
	}

	// Saves a save state to disk.
	public void save() throws IOException {
		String path = "savestates/savestate.xml";
		File saveFile = Gdx.files.internal(path).file();
		PrintWriter printWriter = new PrintWriter(saveFile);
		printWriter.write("<?xml version=\"1.1\" encoding=\"UTF-8\"?>\n");
		XmlWriter xmlWriter = new XmlWriter(printWriter);

		xmlWriter.element("savestate");
		for (int i = 0; i < levels.length; i++) {
			switch (i) {
				case 0: xmlWriter.element("normal"); break;
				case 1:	xmlWriter.element("smart"); break;
				case 2:	xmlWriter.element("genius");
			}
			for (int j = 0; j < levels[i].length; j++) {
				xmlWriter.element("level" + Integer.toString(j+1));
				xmlWriter.text(levels[i][j] == SolveState.SOLVED);
				xmlWriter.pop();
			}
			xmlWriter.pop();
		}
		xmlWriter.pop();
		xmlWriter.close();
		printWriter.close();
	}

	// Resets a save state to no completed levels.
	public void reset() {
		for (int i = 0; i < levels.length; i++) {
			for (int j = 0; j < levels[i].length; j++) {
				levels[i][j] = SolveState.UNSOLVED;
			}
		}
	}

	// Sets the respective level to solved.
	public void setSolved(Difficulty difficulty, int number) {
		switch (difficulty) {
			case NORMAL: levels[0][number-1] = SolveState.SOLVED; break;
			case SMART: levels[1][number-1] = SolveState.SOLVED; break;
			case GENIUS: levels[2][number-1] = SolveState.SOLVED;
		}
	}

	public SolveState getSolveState(Difficulty difficulty, int number) {
		switch (difficulty) {
			case NORMAL: return levels[0][number-1];
			case SMART: return levels[1][number-1];
			case GENIUS: return levels[2][number-1];
			default: return levels[0][number-1];
		}
	}

	public boolean isUnlocked(Difficulty difficulty, int number) {
		if (number == 1) {
			switch (difficulty) {
				case NORMAL: return true;
				case SMART: return getSolveState(Difficulty.NORMAL, 16)
								== SolveState.SOLVED;
				case GENIUS: return getSolveState(Difficulty.SMART, 16)
								== SolveState.SOLVED;
				default: return false;
			}
		} else {
			return (getSolveState(difficulty, number-1) == SolveState.SOLVED);
		}
	}

}

