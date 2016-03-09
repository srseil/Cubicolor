package com.game.mygame;

import com.badlogic.gdx.Game;
import com.badlogic.gdx.audio.Music;
import com.badlogic.gdx.audio.Sound;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.graphics.g2d.TextureAtlas;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelBatch;
import com.badlogic.gdx.graphics.g3d.utils.DefaultShaderProvider;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.utils.Array;

import java.io.IOException;
import java.util.EnumMap;
import java.util.HashMap;

public class MyGame extends Game {

	public static final String GAME_VERSION = "v1.0";

	private Settings settings;
	private ModelBatch modelBatch;
	private AssetLoader assetLoader;
	private LevelLoader levelLoader;
	private SaveState saveState;
	private MenuScreen menuScreen;
	// Assets
	private Model playerModel;
	private Model tileModel;
	private Model exitTileModel;
	private Model keyTileModel;
	private EnumMap<TileColor, Model> lockTileModels;
	private EnumMap<TileColor, TextureAtlas> playerAnimations;
	private EnumMap<TileColor, TextureAtlas.AtlasRegion> keyTileTextures;
	private HashMap<String, TextureAtlas.AtlasRegion> tutorialTextures;
	private HashMap<String, Sound> sounds;
	private Music music;
	private HashMap<String, BitmapFont> bitmapFonts;
	private Skin skin;

	public MyGame() {
		super();
		// Load settings at construction.
		settings = new Settings();
		try {
			settings.load();
		} catch (IOException e) {
			e.printStackTrace();
		}
	}

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

		// Tile, exit tile and key tile model
		tileModel = assetLoader.loadTileModel();
		exitTileModel = assetLoader.loadExitTileModel();
		keyTileModel = assetLoader.loadKeyTileModel();

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

		// Load key tile textures
		Array<TextureAtlas.AtlasRegion> regions =
				assetLoader.loadKeyTileTextures().getRegions();
		keyTileTextures = new EnumMap<>(TileColor.class);
		keyTileTextures.put(TileColor.RED, regions.get(0));
		keyTileTextures.put(TileColor.GREEN, regions.get(1));
		keyTileTextures.put(TileColor.BLUE, regions.get(2));
		keyTileTextures.put(TileColor.YELLOW, regions.get(3));
		keyTileTextures.put(TileColor.NONE, regions.get(4));

		// Tutorial textures
		Array<TextureAtlas.AtlasRegion> tutorial =
				assetLoader.loadTutorialTextures().getRegions();
		tutorialTextures = new HashMap<>();
		tutorialTextures.put("1600x900",  tutorial.get(0));
		tutorialTextures.put("1920x1080", tutorial.get(1));
		tutorialTextures.put("1536x864",  tutorial.get(2));
		tutorialTextures.put("1680x1050", tutorial.get(3));
		tutorialTextures.put("1280x1024", tutorial.get(4));
		tutorialTextures.put("1440x900",  tutorial.get(5));
		tutorialTextures.put("1024x768",  tutorial.get(6));
		tutorialTextures.put("1280x720",  tutorial.get(7));
		tutorialTextures.put("800x600",   tutorial.get(8));
		tutorialTextures.put("1360x768",  tutorial.get(9));
		tutorialTextures.put("1366x768",  tutorial.get(10));
		tutorialTextures.put("1280x800",  tutorial.get(11));

		// Sounds
		sounds = new HashMap<>();
		sounds.put("Player-Step", assetLoader.loadSound("player_step"));
		sounds.put("Player-Coloring", assetLoader.loadSound("coloring"));
		sounds.put("Player-Decoloring", assetLoader.loadSound("decoloring"));
		sounds.put("Tile-Reviving", assetLoader.loadSound("reviving"));
		sounds.put("Button-Click", assetLoader.loadSound("button_click"));
		sounds.put("Level-Solve", assetLoader.loadSound("level_solve"));

		// Music
		music = assetLoader.loadMusic("soundtrack");

		// Bitmap fonts
		bitmapFonts = new HashMap<>();
		bitmapFonts = assetLoader.loadBitmapFonts();

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

		// Start playing music.
		music.setLooping(true);
		if (!settings.getMusicMuted())
			music.play();

		// Load settings & save state and switch to menu screen.
		try {
			saveState.load();
			menuScreen = new MenuScreen(this);
			this.setScreen(menuScreen);
		} catch (IOException exception) {
			System.out.println("Error while loading level.");
			exception.printStackTrace();
		}
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
		keyTileModel.dispose();
		music.dispose();
		for (Sound sound : sounds.values())
			sound.dispose();
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
	public void openLevel(Difficulty difficulty, int n) {
		try {
			Level level = levelLoader.load(difficulty, n);
			GameScreen gameScreen = new GameScreen(level, this);
			this.setScreen(gameScreen);
		} catch (IOException exception) {
			System.out.println("Error while loading level.");
			exception.printStackTrace();
		}
	}

	public void toMenuScreen() {
		menuScreen.getLevelMenu().updateLevelButtons();
		setScreen(menuScreen);
	}

	public ChangeListener createClickListener() {
		return new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				if (!getSettings().getSoundMuted()) {
					getSound("Button-Click").play(
							getSettings().getSoundVolume() / 100.0f);
				}
			}
		};
	}

	public Settings getSettings() {
		return settings;
	}

	public SaveState getSaveState() {
		return saveState;
	}

	public ModelBatch getModelBatch() {
		return modelBatch;
	}

	public Model getPlayerModel() {
		return playerModel;
	}

	public Model getTileModel() {
		return tileModel;
	}

	public Model getKeyTileModel() {
		return keyTileModel;
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

	public TextureAtlas.AtlasRegion getKeyTileTexture(TileColor color) {
		return keyTileTextures.get(color);
	}

	public TextureRegion getTutorialTexture(String name) {
		return tutorialTextures.get(name);
	}

	public Sound getSound(String name) {
		return sounds.get(name);
	}

	public Music getMusic() {
		return music;
	}

	public BitmapFont getBitmapFont(String name) {
		return bitmapFonts.get(name);
	}

	public Skin getSkin() {
		return skin;
	}

}

