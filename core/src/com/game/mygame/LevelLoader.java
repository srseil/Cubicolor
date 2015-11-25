package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.files.FileHandle;
import com.badlogic.gdx.utils.GdxRuntimeException;
import com.badlogic.gdx.utils.XmlReader;

import java.io.IOException;
import java.util.EnumSet;

public class LevelLoader {

    private XmlReader reader;

    public LevelLoader() {
        reader = new XmlReader();
    }

    public Level load(String difficult, int number) throws IOException {
        String filename = "levels/level" + Integer.toString(number) + ".xml";
        FileHandle handle = Gdx.files.internal(filename);
        XmlReader.Element root = reader.parse(handle);

        Difficulty difficulty;
        int optimal, startRow, startColumn;
        EnumSet<TileColor> exitRequirements;
        Tile[][] matrix;

        try {
            String diff = root.get("difficulty");
			switch (diff) {
				case "Normal": difficulty = Difficulty.NORMAL; break;
				case "Smart": difficulty = Difficulty.SMART; break;
				case "Genius": difficulty = Difficulty.GENIUS; break;
				default: difficulty = Difficulty.NORMAL;
			}
            optimal = Integer.parseInt(root.get("optimal"));

            String[] parts = root.get("start").split(",");
            startRow = Integer.parseInt(parts[0]);
            startColumn = Integer.parseInt(parts[1]);

            char[] chars = root.get("requirements").toCharArray();
            exitRequirements = EnumSet.noneOf(TileColor.class);
            for (int i = 0; i < chars.length; i++) {
                TileColor color = parseColor(chars[i]);
                exitRequirements.add(color);
            }

            XmlReader.Element matrixElement = root.getChildByName("matrix");
            if (matrixElement == null)
                throw new IOException();

            int rows = Integer.parseInt(matrixElement.get("rows"));
            int columns = Integer.parseInt(matrixElement.get("columns"));

            matrix = new Tile[rows][columns];
            XmlReader.Element[] lines = matrixElement.getChildrenByName("line").toArray(XmlReader.Element.class);
            if (lines.length == 0)
                throw new IOException();
            char[] row;

            for (int i = lines.length - 1; i >= 0; i--) {
                row = lines[i].getText().toCharArray();
                for (int j = 0; j < row.length; j++) {
                    matrix[i][j] = parseTile(row[j], exitRequirements);
                }
            }
        } catch (GdxRuntimeException exception) {
            exception.printStackTrace();
            throw new IOException();
        }

        return new Level(difficulty, number, optimal,
				startRow, startColumn, exitRequirements, matrix);
    }


    private TileColor parseColor(char identifier) throws IOException {
        switch (identifier) {
            case 'R':
                return TileColor.RED;
            case 'G':
                return TileColor.GREEN;
            case 'B':
                return TileColor.BLUE;
            case 'Y':
                return TileColor.YELLOW;
            default:
                throw new IOException();
        }
    }

    private Tile parseTile(char identifier, EnumSet<TileColor> exitReqs) throws IOException {
        switch (identifier) {
            case '0':
                return new EmptyTile();
            case 'N':
                return new Tile();
            case 'E':
                return new ExitTile(exitReqs.clone());
            case 'R':
                return new LockTile(TileColor.RED);
            case 'r':
                return new KeyTile(TileColor.RED);
            case 'G':
                return new LockTile(TileColor.GREEN);
            case 'g':
                return new KeyTile(TileColor.GREEN);
            case 'B':
                return new LockTile(TileColor.BLUE);
            case 'b':
                return new KeyTile(TileColor.BLUE);
            case 'Y':
                return new LockTile(TileColor.YELLOW);
            case 'y':
                return new KeyTile(TileColor.YELLOW);
            default:
                throw new IOException();
        }
    }

}
