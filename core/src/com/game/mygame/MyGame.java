package com.game.mygame;

import com.badlogic.gdx.Game;
import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.graphics.g2d.TextureAtlas;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelBatch;
import com.badlogic.gdx.graphics.g3d.utils.DefaultShaderProvider;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;

import java.io.IOException;
import java.util.EnumMap;
import java.util.HashMap;

public class MyGame extends Game {

	// Error handling for getters. What happens if maps font have element?

	private ModelBatch modelBatch;
	private AssetLoader assetLoader;
	private LevelLoader levelLoader;
	private SaveState saveState;
	private MenuScreen menuScreen;
	private GameScreen gameScreen;
	// Assets
	private Model playerModel;
	private Model tileModel;
	private Model exitTileModel;
	private EnumMap<TileColor, Model> keyTileModels;
	private EnumMap<TileColor, Model> lockTileModels;
	private EnumMap<TileColor, TextureAtlas> playerAnimations;
	private HashMap<String, BitmapFont> bitmapFonts;
	private Skin skin;

	public MyGame() {}

	private void loadAssets() {
		// Player model
		playerModel = assetLoader.loadPlayerModel();

		// Player animations
		playerAnimations = new EnumMap<>(TileColor.class);
		playerAnimations.put(TileColor.RED,
				assetLoader.loadPlayerAnimation(TileColor.RED));
		playerAnimations.put(TileColor.GREEN,
				assetLoader.loadPlayerAnimation(TileColor.GREEN));
		playerAnimations.put(TileColor.BLUE,
				assetLoader.loadPlayerAnimation(TileColor.BLUE));
		playerAnimations.put(TileColor.YELLOW,
				assetLoader.loadPlayerAnimation(TileColor.YELLOW));
		playerAnimations.put(TileColor.NONE,
				assetLoader.loadPlayerAnimation(TileColor.NONE));

		// Tile and exit tile model
		tileModel = assetLoader.loadTileModel();
		exitTileModel = assetLoader.loadExitTileModel();

		// Key tile models
		keyTileModels = new EnumMap<>(TileColor.class);
		keyTileModels.put(TileColor.RED,
				assetLoader.loadKeyTileModel(TileColor.RED));
		keyTileModels.put(TileColor.GREEN,
				assetLoader.loadKeyTileModel(TileColor.GREEN));
		keyTileModels.put(TileColor.BLUE,
				assetLoader.loadKeyTileModel(TileColor.BLUE));
		keyTileModels.put(TileColor.YELLOW,
				assetLoader.loadKeyTileModel(TileColor.YELLOW));

		// Lock tile models
		lockTileModels = new EnumMap<>(TileColor.class);
		lockTileModels.put(TileColor.RED,
				assetLoader.loadLockTileModel(TileColor.RED));
		lockTileModels.put(TileColor.GREEN,
				assetLoader.loadLockTileModel(TileColor.GREEN));
		lockTileModels.put(TileColor.BLUE,
				assetLoader.loadLockTileModel(TileColor.BLUE));
		lockTileModels.put(TileColor.YELLOW,
				assetLoader.loadLockTileModel(TileColor.YELLOW));

		// Bitmap fonts
		bitmapFonts = new HashMap<>();
		bitmapFonts.put("OldStandard-Regular-30",
				assetLoader.loadBitmapFont("OldStandard-Regular-30"));
		bitmapFonts.put("OldStandard-Regular-50",
				assetLoader.loadBitmapFont("OldStandard-Regular-50"));
		bitmapFonts.put("OldStandard-Regular-40",
				assetLoader.loadBitmapFont("OldStandard-Regular-40"));
		bitmapFonts.put("OldStandard-Regular-60",
				assetLoader.loadBitmapFont("OldStandard-Regular-60"));

		// Skin
		skin = assetLoader.loadSkin("uiskin");
	}

	@Override
	public void create() {
		modelBatch = new ModelBatch(new DefaultShaderProvider());
		assetLoader = new AssetLoader();
		levelLoader = new LevelLoader();
		saveState = new SaveState();

		loadAssets();

		// Create single instances of menu and game screen here?

		// Load save state and switch to menu screen.
		try {
			saveState.load();
			menuScreen = new MenuScreen(this);
			//this.setScreen(menuScreen);
			//saveState.save();
			openLevel("normal", 1);
		} catch (IOException exception) {
			System.out.println("Error while loading level.");
			exception.printStackTrace();
		}

		// For debugging purposes
		System.out.println("Level 1 solve state: " + saveState.getSolveState(Difficulty.NORMAL, 1));
		System.out.println("Level 2 solve state: " + saveState.getSolveState(Difficulty.NORMAL, 2));
		System.out.println("Level 3 solve state: " + saveState.getSolveState(Difficulty.NORMAL, 3));
	}

	@Override
	public void render() {
		super.render();
	}

	@Override
	public void dispose() {
		modelBatch.dispose();
		skin.dispose();
		playerModel.dispose();
		tileModel.dispose();
		exitTileModel.dispose();
		for (Model model : keyTileModels.values())
			model.dispose();
		for (Model model : lockTileModels.values())
			model.dispose();
		for (TextureAtlas atlas : playerAnimations.values())
			atlas.dispose();
		for (BitmapFont font : bitmapFonts.values())
			font.dispose();
	}

	/*
	 * Open the level with the specified difficulty and number.
	 */
	public void openLevel(String difficulty, int n) {
		try {
			Level level = levelLoader.load(difficulty, n);
			gameScreen = new GameScreen(level, this);
			this.setScreen(gameScreen);
		} catch (IOException exception) {
			System.out.println("Error while loading level.");
			exception.printStackTrace();
		}
	}

	// Change screen methods...
	public void toGameScreen() {
		this.setScreen(gameScreen);
	}

	public MenuScreen getMenuScreen() {
		return menuScreen;
	}
	// ---

	// Change to save() and load() or something?
	public SaveState getSaveState() {
		return saveState;
	}
	// ---

	// Change to start & end methods?
	public ModelBatch getModelBatch() {
		return modelBatch;
	}
	// ---

	public Model getPlayerModel(TileColor key) {
		return playerModel;
	}

	public Model getTileModel() {
		return tileModel;
	}

	public Model getKeyTileModel(TileColor color) {
		return keyTileModels.get(color);
	}

	public Model getLockTileModel(TileColor color) {
		return lockTileModels.get(color);
	}

	public Model getExitTileModel() {
		return exitTileModel;
	}

	public TextureAtlas getPlayerAnimation(TileColor color) {
		return playerAnimations.get(color);
	}

	public BitmapFont getBitmapFont(String name) {
		return bitmapFonts.get(name);
	}

	public Skin getSkin() {
		return skin;
	}

}

