package com.game.mygame;

import com.badlogic.gdx.Game;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.graphics.g2d.SpriteBatch;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

import java.io.IOException;

public class MyGame extends Game {

	private OrthographicCamera camera;
	private SpriteBatch batch;
	private ShapeRenderer shapeRenderer;
	private BitmapFont font;

	private MenuScreen menuScreen;
	private GameScreen gameScreen;

	public void create() {
		camera = new OrthographicCamera(800, 600);
		camera.setToOrtho(false);

		batch = new SpriteBatch();
		shapeRenderer = new ShapeRenderer();
		font = new BitmapFont();

		LevelLoader loader = new LevelLoader();
		Level level;
		Player player;

		try {
			level = loader.load("level.xml");
			player = new Player(level);
			gameScreen = new GameScreen(level, player, camera, this);
			menuScreen = new MenuScreen(camera, this);
			this.setScreen(menuScreen);
		} catch (IOException exception) {
			System.out.println("Error while loading level.");
			exception.printStackTrace();
		}

		/*
		Level level = new Level("");
		level.rows = 6;
		level.columns = 2;
		level.matrix = new Tile[6][2];
		level.matrix[0][0] = new Tile();
		level.matrix[0][1] = new EmptyTile();
		level.matrix[1][0] = new LockTile(TileColor.RED);
		level.matrix[1][1] = new KeyTile(TileColor.RED);
		level.matrix[2][0] = new LockTile(TileColor.GREEN);
		level.matrix[2][1] = new KeyTile(TileColor.GREEN);
		level.matrix[3][0] = new LockTile(TileColor.BLUE);
		level.matrix[3][1] = new KeyTile(TileColor.BLUE);
		level.matrix[4][0] = new LockTile(TileColor.YELLOW);
		level.matrix[4][1] = new KeyTile(TileColor.YELLOW);
		level.matrix[5][0] = new EmptyTile();
		level.matrix[5][1] = new ExitTile(4);
		*/

	}

	public void render() {
		super.render();
	}

	public void dispose() {
		batch.dispose();
		shapeRenderer.dispose();
		font.dispose();
	}

	public void toGameScreen() {
		this.setScreen(gameScreen);
	}

	public SpriteBatch getSpriteBatch() {
		return batch;
	}

	public ShapeRenderer getShapeRenderer() {
		return shapeRenderer;
	}

	public BitmapFont getFont() {
		return font;
	}

	public MenuScreen getMenuScreen() {
		return menuScreen;
	}

	public GameScreen getGameScreen() {
		return gameScreen;
	}

}
