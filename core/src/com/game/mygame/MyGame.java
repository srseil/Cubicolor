package com.game.mygame;

import com.badlogic.gdx.Game;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.graphics.VertexAttributes;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.graphics.g2d.SpriteBatch;
import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelBatch;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.utils.DefaultShaderProvider;
import com.badlogic.gdx.graphics.g3d.utils.ModelBuilder;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;

import java.io.IOException;

public class MyGame extends Game {

	private OrthographicCamera camera;
	private SpriteBatch batch;
	private ShapeRenderer shapeRenderer;
	private BitmapFont font;

	private ModelBuilder modelBuilder;
	private ModelBatch modelBatch;

	private LevelLoader levelLoader;

	private MenuScreen menuScreen;
	private GameScreen gameScreen;
	private SaveState saveState;

	private Model tileModel;
	private Model keyTileRedModel;
	private Model keyTileGreenModel;
	private Model keyTileBlueModel;
	private Model keyTileYellowModel;
	private Model lockTileRedModel;
	private Model lockTileGreenModel;
	private Model lockTileBlueModel;
	private Model lockTileYellowModel;
	private Model exitTileModel;

	private Model playerModel;
	private Model playerRedModel;
	private Model playerGreenModel;
	private Model playerBlueModel;
	private Model playerYellowModel;

	public void create() {
		modelBatch = new ModelBatch(new DefaultShaderProvider());
		modelBuilder = new ModelBuilder();


		tileModel = createTileModel(TileAttributes.TKind.NORMAL, null);
		keyTileRedModel = createTileModel(TileAttributes.TKind.KEY,
				TileAttributes.TColor.RED);
		keyTileGreenModel = createTileModel(TileAttributes.TKind.KEY,
				TileAttributes.TColor.GREEN);
		keyTileBlueModel = createTileModel(TileAttributes.TKind.KEY,
				TileAttributes.TColor.BLUE);
		keyTileYellowModel = createTileModel(TileAttributes.TKind.KEY,
				TileAttributes.TColor.YELLOW);
		lockTileRedModel = createTileModel(TileAttributes.TKind.LOCK,
				TileAttributes.TColor.RED);
		lockTileGreenModel = createTileModel(TileAttributes.TKind.LOCK,
				TileAttributes.TColor.GREEN);
		lockTileBlueModel = createTileModel(TileAttributes.TKind.LOCK,
				TileAttributes.TColor.BLUE);
		lockTileYellowModel = createTileModel(TileAttributes.TKind.LOCK,
				TileAttributes.TColor.YELLOW);
		exitTileModel = createTileModel(TileAttributes.TKind.EXIT, null);
		/*
		playerModel = getModelBuilder().createBox(10.0f, 10.0f, 10.0f,
				new Material(ColorAttribute.createDiffuse(Color.DARK_GRAY)),
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
		*/
		playerModel = createPlayerModel(TileAttributes.TColor.NONE);
		playerRedModel = createPlayerModel(TileAttributes.TColor.RED);
		playerGreenModel = createPlayerModel(TileAttributes.TColor.GREEN);
		playerBlueModel = createPlayerModel(TileAttributes.TColor.BLUE);
		playerYellowModel = createPlayerModel(TileAttributes.TColor.YELLOW);



		camera = new OrthographicCamera(800, 600);
		camera.setToOrtho(false);

		batch = new SpriteBatch();
		shapeRenderer = new ShapeRenderer();
		font = new BitmapFont();

		levelLoader = new LevelLoader();
		Level level;
		Player player;

		saveState = new SaveState();

		try {
			//level = levelLoader.load("level1.xml");
			//player = new Player(level, gameScreen);
			//gameScreen = new GameScreen(level, camera, this);
			saveState.load();
			menuScreen = new MenuScreen(camera, this);
			//this.setScreen(menuScreen);
			saveState.save();
			openLevel("normal", 1);
			//this.setScreen(new TestScreen(this));
		} catch (IOException exception) {
			System.out.println("Error while loading level.");
			exception.printStackTrace();
		}

		System.out.println(saveState.getSolveState(Difficulty.NORMAL, 1));
		System.out.println(saveState.getSolveState(Difficulty.NORMAL, 2));
		System.out.println(saveState.getSolveState(Difficulty.NORMAL, 3));
	}

	public void render() {
		super.render();
	}

	public void dispose() {
		batch.dispose();
		shapeRenderer.dispose();
		font.dispose();
	}

	public void openLevel(String difficulty, int n) {
		try {
			Level level = levelLoader.load(difficulty, n);
			gameScreen = new GameScreen(level, camera, this);
			this.setScreen(gameScreen);
		} catch (IOException exception) {
			System.out.println("Error while loading level.");
			exception.printStackTrace();
		}
	}

	public void toGameScreen() {
		this.setScreen(gameScreen);
	}

	private Model createTileModel(TileAttributes.TKind kind,
								  TileAttributes.TColor color) {
		Material material = new Material();
		if (color != null) {
			material.set(ColorAttribute.createDiffuse(
					TileAttributes.getGDXColor(color)));
		} else {
			material.set(ColorAttribute.createDiffuse(Color.WHITE));
		}
		Model model = getModelBuilder().createBox(10.0f, 5.0f, 10.0f, material,
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
		/*
		switch (kind) {
			case NORMAL:
		}
		*/
		return model;
	}

	private Model createPlayerModel(TileAttributes.TColor key) {
		Material material = new Material();
		if (key != TileAttributes.TColor.NONE) {
			material.set(ColorAttribute.createDiffuse(
					TileAttributes.getGDXColor(key)));
		} else {
			material.set(ColorAttribute.createDiffuse(Color.DARK_GRAY));
		}
		Model model = getModelBuilder().createBox(10.0f, 10.0f, 10.0f, material,
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
		return model;
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

	public SaveState getSaveState() {
		return saveState;
	}

	public ModelBuilder getModelBuilder() {
		return modelBuilder;
	}

	public ModelBatch getModelBatch() {
		return modelBatch;
	}

	public Model getTileModel() {
		return tileModel;
	}

	public Model getKeyTileModel(TileAttributes.TColor color) {
		switch (color) {
			case RED: return keyTileRedModel;
			case GREEN: return keyTileGreenModel;
			case BLUE: return keyTileBlueModel;
			case YELLOW: return keyTileYellowModel;
			default: return tileModel;
		}
	}

	public Model getLockTileModel(TileAttributes.TColor color) {
		switch (color) {
			case RED: return lockTileRedModel;
			case GREEN: return lockTileGreenModel;
			case BLUE: return lockTileBlueModel;
			case YELLOW: return lockTileYellowModel;
			default: return tileModel;
		}
	}

	public Model getPlayerModel(TileAttributes.TColor key) {
		switch (key) {
			case RED: return playerRedModel;
			case GREEN: return playerGreenModel;
			case BLUE: return playerBlueModel;
			case YELLOW: return playerYellowModel;
			default: return playerModel;
		}
	}

	public Model getExitTileModel() {
		return exitTileModel;
	}

}
