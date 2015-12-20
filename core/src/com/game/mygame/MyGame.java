package com.game.mygame;

import com.badlogic.gdx.Game;
import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.assets.loaders.ModelLoader;
import com.badlogic.gdx.graphics.*;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.graphics.g2d.SpriteBatch;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelBatch;
import com.badlogic.gdx.graphics.g3d.attributes.BlendingAttribute;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.attributes.TextureAttribute;
import com.badlogic.gdx.graphics.g3d.loader.G3dModelLoader;
import com.badlogic.gdx.graphics.g3d.utils.DefaultShaderProvider;
import com.badlogic.gdx.graphics.g3d.utils.ModelBuilder;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;
import com.badlogic.gdx.utils.UBJsonReader;

import java.io.IOException;

public class MyGame extends Game {

	private OrthographicCamera camera;
	private SpriteBatch batch;
	private ShapeRenderer shapeRenderer;

	private ModelBuilder modelBuilder;
	private ModelBatch modelBatch;

	private LevelLoader levelLoader;
	public ModelLoader modelLoader;

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

	private BitmapFont font;
	private BitmapFont bitmapOSR30;
	private BitmapFont bitmapOSR60;

	@Override
	public void create() {
		modelLoader = new G3dModelLoader(new UBJsonReader());
		modelBatch = new ModelBatch(new DefaultShaderProvider());
		modelBuilder = new ModelBuilder();

		// Fonts.
		bitmapOSR30 = new BitmapFont(Gdx.files.internal("fonts/OldStandard-Regular-30.fnt"));
		bitmapOSR60 = new BitmapFont(Gdx.files.internal("fonts/OldStandard-Regular-60.fnt"));

		tileModel = createTileModel();
		keyTileRedModel = createKeyTileModel(TileColor.RED);
		keyTileGreenModel = createKeyTileModel(TileColor.GREEN);
		keyTileBlueModel = createKeyTileModel(TileColor.BLUE);
		keyTileYellowModel = createKeyTileModel(TileColor.YELLOW);
		lockTileRedModel = createLockTileModel(TileColor.RED);
		lockTileGreenModel = createLockTileModel(TileColor.GREEN);
		lockTileBlueModel = createLockTileModel(TileColor.BLUE);
		lockTileYellowModel = createLockTileModel(TileColor.YELLOW);

		exitTileModel = modelLoader.loadModel(Gdx.files.internal("ExitTile.g3db"));
		exitTileModel.materials.first().set(new BlendingAttribute(true, 1.0f));
		exitTileModel.materials.first().set(ColorAttribute.createDiffuse(Color.GOLD));



		/*
		playerModel = getModelBuilder().createBox(10.0f, 10.0f, 10.0f,
				new Material(ColorAttribute.createDiffuse(Color.DARK_GRAY)),
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
		*/
		playerModel = createPlayerModel(TileColor.NONE);
		playerRedModel = createPlayerModel(TileColor.RED);
		playerGreenModel = createPlayerModel(TileColor.GREEN);
		playerBlueModel = createPlayerModel(TileColor.BLUE);
		playerYellowModel = createPlayerModel(TileColor.YELLOW);



		camera = new OrthographicCamera(Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		camera.setToOrtho(false);

		/*
		batch = new SpriteBatch();
		shapeRenderer = new ShapeRenderer();
		font = new BitmapFont(Gdx.files.internal("OldStandard-Regular-30.fnt"));
		*/

		levelLoader = new LevelLoader();
		saveState = new SaveState();

		try {
			//level = levelLoader.load("level1.xml");
			//player = new Player(level, gameScreen);
			//gameScreen = new GameScreen(level, camera, this);
			saveState.load();
			menuScreen = new MenuScreen(this);
			//this.setScreen(menuScreen);
			//saveState.save();
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

	@Override
	public void render() {
		super.render();
	}

	@Override
	public void dispose() {
		/*
		batch.dispose();
		shapeRenderer.dispose();
		font.dispose();
		*/
		bitmapOSR30.dispose();
		bitmapOSR60.dispose();
	}

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

	public void toGameScreen() {
		this.setScreen(gameScreen);
	}

	private Model createTileModel() {
		Model model = modelLoader.loadModel(Gdx.files.internal("Tile.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(
				TileColor.getGdxColor(TileColor.NONE)));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	private Model createKeyTileModel(TileColor color) {
		Model model = modelLoader.loadModel(Gdx.files.internal("KeyTile.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		TextureAttribute textureAttribute = model.materials.first().get(
				TextureAttribute.class, TextureAttribute.Diffuse);
		Texture texture = new Texture(Gdx.files.internal(
				TileColor.getKeyTileTexturePath(color)));
		//texture = new Texture(Gdx.files.internal("keytile_red.png"));
		texture = new Texture(Gdx.files.internal("keytile_red.png"));
		textureAttribute.set(new TextureRegion(texture));
		/*
			material.set(ColorAttribute.createDiffuse(Color.WHITE));
			//model.materials.first().set(ColorAttribute.createDiffuse(new Color(0.91f, 0.902f, 0.875f, 1.0f)));
			//model.materials.first().set(ColorAttribute.createDiffuse(new Color(0.949f, 0.941f, 0.925f, 1.0f)));

			model = modelLoader.loadModel(Gdx.files.internal("Tile.g3db"));
			model.materials.first().set(ColorAttribute.createDiffuse(new Color(0.989f, 0.981f, 0.965f, 1.0f)));
			model.materials.first().set(new BlendingAttribute(true, 1.0f));
			*/

		//model.materials.removeIndex(0);
		//model.materials.add(material);
		/*
		model = getModelBuilder().createBox(10.0f, 5.0f, 10.0f, material,
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
				*/
		/*
		switch (kind) {
			case NORMAL:
		}
		*/
		return model;
	}

	private Model createLockTileModel(TileColor color) {
		Model model = modelLoader.loadModel(Gdx.files.internal("Tile.g3db"));
		model.materials.first().set(ColorAttribute.createDiffuse(
				TileColor.getGdxColor(color)));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));
		return model;
	}

	private Model createPlayerModel(TileColor key) {
		Material material = new Material();
		if (key != TileColor.NONE) {
			/*
			material.set(ColorAttribute.createDiffuse(
					TileColor.getGdxColor(key)));
					*/
		} else {
			//material.set(ColorAttribute.createDiffuse(Color.DARK_GRAY));
		}
		/*
		Model model = getModelBuilder().createBox(10.0f, 10.0f, 10.0f, material,
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
		*/

		Model model = modelLoader.loadModel(Gdx.files.internal("Player.g3db"));
		//model.materials.first().set(ColorAttribute.createDiffuse(Color.DARK_GRAY));
		model.materials.first().set(ColorAttribute.createDiffuse(Color.WHITE));
		model.materials.first().set(new BlendingAttribute(true, 1.0f));


		Cubemap cubeMap = new Cubemap(Gdx.files.internal("cube.jpg"),
				Gdx.files.internal("cube.jpg"),Gdx.files.internal("cube.jpg"),
				Gdx.files.internal("cube.jpg"),Gdx.files.internal("cube.jpg"),
				Gdx.files.internal("cube.jpg"));

		Texture texture = new Texture(Gdx.files.internal("cube.jpg"));

		//model.materials.first().set(new IntAttribute(IntAttribute.CullFace, Gdx.gl.GL_FALSE));
		System.out.println("TEXTURE: " + model.materials.first().has(TextureAttribute.Diffuse));
		//model.materials.first().set(new TextureAttribute(TextureAttribute.Diffuse));
		//model.materials.first().set(new CubemapAttribute());


		//model.materials.first().set(new VertexAttribute(VertexAttributes.Usage.TextureCoordinates, 2, "a_textCoords"));//new TextureAttribute(TextureAttribute.Diffuse, texture));

		return model;
	}

	/*
	public SpriteBatch getSpriteBatch() {
		return batch;
	}

	public ShapeRenderer getShapeRenderer() {
		return shapeRenderer;
	}
	*/

	public BitmapFont getBitmapFont(String name) {
		switch (name) {
			case "OldStandard-Regular-30":
				return bitmapOSR30;
			case "OldStandard-Regular-60":
				return bitmapOSR60;
			default:
				return bitmapOSR30;
		}
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

	public Model getKeyTileModel(TileColor color) {
		switch (color) {
			case RED: return keyTileRedModel;
			case GREEN: return keyTileGreenModel;
			case BLUE: return keyTileBlueModel;
			case YELLOW: return keyTileYellowModel;
			default: return tileModel;
		}
	}

	public Model getLockTileModel(TileColor color) {
		switch (color) {
			case RED: return lockTileRedModel;
			case GREEN: return lockTileGreenModel;
			case BLUE: return lockTileBlueModel;
			case YELLOW: return lockTileYellowModel;
			default: return tileModel;
		}
	}

	public Model getPlayerModel(TileColor key) {
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
