package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.files.FileHandle;
import com.badlogic.gdx.utils.GdxRuntimeException;
import com.badlogic.gdx.utils.XmlReader;
import com.badlogic.gdx.utils.XmlWriter;

import java.io.File;
import java.io.IOException;
import java.io.PrintWriter;
import java.io.StringWriter;

public class SaveState {

	private XmlReader reader;
	private SolveState levels[][];

	public SaveState() {
		reader = new XmlReader();
		levels = new SolveState[3][3];
	}

	// Loads a save state from disk.
	public void load() throws IOException {
		FileHandle handle = Gdx.files.internal("savestate.xml");
		XmlReader.Element root = reader.parse(handle);

		try {
			XmlReader.Element normal = root.getChildByName("normal");
			XmlReader.Element smart = root.getChildByName("smart");
			XmlReader.Element genius = root.getChildByName("genius");
			XmlReader.Element diffElement = null;

			for (int i = 0; i < levels.length; i++) {
				switch (i) {
					case 0: diffElement = root.getChildByName("normal"); break;
					case 1: diffElement = root.getChildByName("smart"); break;
					case 2: diffElement = root.getChildByName("genius"); break;
					//default: root.getChildByName("normal");
				}
				for (int j = 0; j < levels[i].length; j++) {
					switch (diffElement.get("level" + Integer.toString(i+1))) {
						case "unsolved":
							levels[i][j] = SolveState.UNSOLVED;
							break;
						case "solved":
							levels[i][j] = SolveState.SOLVED;
							break;
						case "optimal":
							levels[i][j] = SolveState.OPTIMAL;
							break;
						default:
							throw new IOException();
					}
				}
			}
		} catch (GdxRuntimeException exception) {
			exception.printStackTrace();
			throw new IOException();
		}
	}

	/*
	// Saves a save state to disk.
	public void save() throws IOException {
		File saveFile = Gdx.files.internal("savestate.xml").file();
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
				switch (levels[i][j]) {
					case UNSOLVED: xmlWriter.text("unsolved"); break;
					case SOLVED: xmlWriter.text("solved"); break;
					case OPTIMAL: xmlWriter.text("optimal");
				}
				xmlWriter.pop();
			}
			xmlWriter.pop();
		}
		xmlWriter.pop();
		xmlWriter.close();
		printWriter.close();
	}
	*/

	// Updates the save state with the corresponding completed level.
	public void update(Difficulty difficulty, int number, boolean optimal) {
		SolveState state;
		if (optimal)
			state = SolveState.OPTIMAL;
		else
			state = SolveState.SOLVED;

		switch (difficulty) {
			case NORMAL: levels[0][number-1] = state;
			case SMART: levels[1][number-1] = state;
			case GENIUS: levels[2][number-1] = state;
			default: levels[0][number-1] = state;
		}
	}

	// Resets a save state to no completed levels.
	public void reset() {
		for (int i = 0; i < levels.length; i++) {
			for (int j = 0; j < levels[i].length; j++) {
				levels[i][j] = SolveState.UNSOLVED;
			}
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
}
