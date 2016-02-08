package com.game.mygame;

import com.badlogic.gdx.Game;
import com.badlogic.gdx.audio.Music;
import com.badlogic.gdx.audio.Sound;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.Texture;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.graphics.g2d.NinePatch;
import com.badlogic.gdx.graphics.g2d.TextureAtlas;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelBatch;
import com.badlogic.gdx.graphics.g3d.utils.DefaultShaderProvider;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.utils.NinePatchDrawable;
import com.badlogic.gdx.scenes.scene2d.utils.TextureRegionDrawable;
import com.badlogic.gdx.utils.Array;

import java.io.IOException;
import java.util.EnumMap;
import java.util.HashMap;

public class MyGame extends Game {

	private Settings settings;
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
	private Model keyTileModel;
	private EnumMap<TileColor, Model> lockTileModels;
	private EnumMap<TileColor, TextureAtlas> playerAnimations;
	private EnumMap<TileColor, TextureAtlas.AtlasRegion> keyTileTextures;
	private TextureAtlas.AtlasRegion exitTileTexture;
	private NinePatchDrawable menuBackground;
	private HashMap<String, Sound> sounds;
	private Music music;
	private HashMap<String, BitmapFont> bitmapFonts;
	private Skin skin;
	/*
	// Control attributes
	private boolean soundMuted;
	private float soundVolume;
	*/

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

		// Exit tile texture
		exitTileTexture = assetLoader.loadExitTileTexture();

		// Sounds
		sounds = new HashMap<>();
		sounds.put("Player-Step", assetLoader.loadSound("player_step"));
		sounds.put("Player-Coloring", assetLoader.loadSound("coloring"));
		sounds.put("Player-Decoloring", assetLoader.loadSound("decoloring"));
		sounds.put("Tile-Reviving", assetLoader.loadSound("reviving"));
		/*
		soundMuted = settings.getSoundMuted();
		soundVolume = settings.getSoundVolume() / 100.0f;
		*/

		// Music
		music = assetLoader.loadMusic("soundtrack");

		// Bitmap fonts
		bitmapFonts = new HashMap<>();
		bitmapFonts = assetLoader.loadBitmapFonts();

		// Skin
		skin = assetLoader.loadSkin("uiskin");

		/*
		// Menu background
		NinePatch patch = skin.get("menu-background", NinePatch.class);
		menuBackground = new NinePatchDrawable(patch);
		*/
	}

	@Override
	public void create() {
		modelBatch = new ModelBatch(new DefaultShaderProvider());
		assetLoader = new AssetLoader();
		levelLoader = new LevelLoader();
		saveState = new SaveState();

		loadAssets();

		// Create single instances of menu and game screen here?

		// Start playing music.
		if (!settings.getMusicMuted())
			music.play();

		// Load settings & save state and switch to menu screen.
		try {
			saveState.load();
			menuScreen = new MenuScreen(this);
			this.setScreen(menuScreen);
			//saveState.save();
			//openLevel("normal", 1);
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

	/*
	public void setSoundMuted(boolean muted) {
		soundMuted = muted;
	}

	public void setSoundVolume(float volume) {
		soundVolume = volume;
	}
	*/

	public void toMenuScreen() {
		menuScreen.getLevelMenu().updateLevelButtons();
		setScreen(menuScreen);
	}

	public Settings getSettings() {
		return settings;
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

	public TextureAtlas.AtlasRegion getExitTileTexture() {
		return exitTileTexture;
	}

	public NinePatchDrawable getMenuBackground() {
		return menuBackground;
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

