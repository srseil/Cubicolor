package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.*;
import com.badlogic.gdx.graphics.g2d.Batch;
import com.badlogic.gdx.graphics.g3d.Environment;
import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.environment.DirectionalLight;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;
import com.badlogic.gdx.math.Quaternion;
import com.badlogic.gdx.math.Vector3;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.sun.corba.se.spi.activation.TCPPortHelper;

public class GameBoard extends Actor implements AnimationController.AnimationListener {

	// animationcontroller update in draw: nicht thread safe? bessere lösung finden...

	// TODO: Animation in Blender ändern -> Eine einzelne, nur Rotation, nicht Translation.

	private MyGame game;
	private Level level;
	private Player player;
	private Environment environment;
	private Model model;
	private ModelInstance modelInstance;
	private ModelInstance[][] matrix;
	private ModelInstance playerModel;
	private ModelInstance playerNormalModel;
	private ModelInstance playerRedModel;
	private ModelInstance playerGreenModel;
	private ModelInstance playerBlueModel;
	private ModelInstance playerYellowModel;
	private float width, height;

	private AnimationController playerAnimation;
	private AnimationController currentPlayerAnimation;
	private AnimationController playerForwardAnimation;
	private AnimationController playerBackAnimation;
	private AnimationController playerRightAnimation;
	private AnimationController playerLeftAnimation;

	private TileAttributes.TColor oldPlayerKey;

	private boolean playerAnimating;

	private float[] newTransform;

	//private PerspectiveCamera camera;
	private OrthographicCamera camera;
	private ShapeRenderer shapeRenderer;

	private boolean moving;

	public GameBoard(Level level, Player player, OrthographicCamera camera, MyGame game, ShapeRenderer shapeRenderer) {
		this.level = level;
		this.player = player;
		this.game = game;
		this.camera = camera;

		this.shapeRenderer = shapeRenderer;

		width = (float) level.getColumns() * 10;
		height = (float) level.getRows() * 10;

		matrix = parseMatrix(level.getMatrix());

		camera.position.set(0, 25.0f, height/2);
		camera.zoom = 0.12f;
		camera.update();

		environment = new Environment();
		environment.set(new ColorAttribute(ColorAttribute.AmbientLight, .4f, .4f, .4f, 1f));
		environment.add(new DirectionalLight().set(0.8f, 0.8f, 0.8f, -0.5f, -1f, 1f));

		//model = game.getModelBuilder().createBox(10f, 5f, 10f,
		//		new Material(ColorAttribute.createDiffuse(Color.BLUE)),
		//		VertexAttributes.Usage.Position
		//		| VertexAttributes.Usage.Normal);
		//modelInstance = new ModelInstance(model);

		playerNormalModel = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.NONE));
		playerRedModel = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.RED));
		playerGreenModel = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.GREEN));
		playerBlueModel = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.BLUE));
		playerYellowModel = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.YELLOW));
		//playerModel.transform.translate(1.0f, 5.0f, 10.0f);
		playerModel = playerNormalModel;

		oldPlayerKey = player.getKey();

		playerForwardAnimation = new AnimationController(playerModel);
		playerAnimation = new AnimationController(playerModel);
		//animationController.setAnimation("Cube|CubeMovement", -1, 2.0f, null);

		playerModel.transform.setTranslation(
				-width / 2 + player.getX() * 10.0f, 7.5f,
				height / 2 - player.getY() * 10.0f);

		playerAnimating = false;
		//playerModel.transform.setTranslation(0, 7.5f, 0);
		//playerModel.transform.setToRotation(0, 1, 0, 90);
		playerModel.transform.setTranslation(
				-width / 2 + player.getX() * 10.0f, 7.5f,
				height / 2 - player.getY() * 10.0f);
		//playerAnimation.setAnimation("Cube|Movement", -1, this);
	}

	@Override
	public void draw(Batch batch, float parentAlpha) {
		batch.end();
		shapeRenderer.end();

		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		shapeRenderer.setProjectionMatrix(batch.getProjectionMatrix());
		shapeRenderer.setTransformMatrix(batch.getTransformMatrix());
		//shapeRenderer.rect(getX(), getY(), 50, 50);

		/*
		for (int i = 0; i < level.getRows(); i++) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!level.getMatrix()[i][j].isDead()) {
					//level.getMatrix()[i][j].draw(getX() - width/2 + j*50.0f,
					//		getY() - height/2 + i*50.0f, shapeRenderer);
		*/



		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);

		game.getModelBatch().begin(camera);
		//game.getModelBatch().render(modelInstance);

		for (int i = 0; i < level.getRows(); i++) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!level.getMatrix()[i][j].isDead()) {
					/*
					ModelInstance tileInstance = new ModelInstance(model,
							-(width/2) + j*10.0f, 0, (height/2) - i*10.0f);
					game.getModelBatch().render(tileInstance, environment);
					*/
					game.getModelBatch().render(matrix[i][j], environment);
				}
			}
		}

		//player.draw(getX() - width/2, getY() - height/2, shapeRenderer);

		/*
		// Update player color.
		if (player.getKey() != oldPlayerKey) {
			playerModel = getPlayerModelInstance(player.getKey());
			oldPlayerKey = player.getKey();
		}
		*/

		// Play animation.
		//if (playerAnimation.) {
		//if (playerAnimation.inAction)

		/*
		if (playerAnimating) {
			System.out.println(playerAnimation.current);
			try {
			} catch (Exception e) {
				System.out.println(playerAnimation.current);
			}
		}
		*/
		if (playerAnimating)
			playerAnimation.update(Gdx.graphics.getDeltaTime());

		//}

		// Set position of player model.
		/*
		playerModel.transform.setTranslation(
				-width/2 + player.getX() * 10.0f, 7.5f,
				height/2 - player.getY() * 10.0f);
				*/
		game.getModelBatch().render(playerModel, environment);


		game.getModelBatch().end();

		shapeRenderer.end();
		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		batch.begin();
	}

	private ModelInstance[][] parseMatrix(Tile[][] tileMatrix) {
		ModelInstance[][] modelMatrix =
				new ModelInstance[tileMatrix.length][tileMatrix[0].length];

		for (int i = 0; i < tileMatrix.length; i++) {
			for (int j = 0; j < tileMatrix[0].length; j++) {
				if (tileMatrix[i][j] instanceof KeyTile) {
					KeyTile keyTile = (KeyTile) tileMatrix[i][j];
					modelMatrix[i][j] = new ModelInstance(
							game.getKeyTileModel(keyTile.getColor()));
				} else if (tileMatrix[i][j] instanceof LockTile) {
					LockTile lockTile = (LockTile) tileMatrix[i][j];
					modelMatrix[i][j] = new ModelInstance(
							game.getLockTileModel(lockTile.getColor()));
				} else if (tileMatrix[i][j] instanceof ExitTile) {
					modelMatrix[i][j] = new ModelInstance(
							game.getExitTileModel());
				} else {
					modelMatrix[i][j] = new ModelInstance(
							game.getTileModel());
				}
				modelMatrix[i][j].transform.setTranslation(
						-width/2 + j*10.0f, 0.0f, height/2 - i*10.0f);
			}
		}

		return modelMatrix;
	}

	public void triggerPlayerMovement(int x, int y, boolean moved) {

		if (x == 0 && y == 1) {
			playerModel.transform.setToRotation(0, 1, 0, 0);
			if (moved) updatePlayerModelTransform(0, -1);
		} else if (x == 0 && y == -1) {
			playerModel.transform.setToRotation(0, 1, 0, 180);
			if (moved) updatePlayerModelTransform(0, 1);
		} else if (x == 1 && y == 0) {
			playerModel.transform.setToRotation(0, 1, 0, 270);
			if (moved) updatePlayerModelTransform(-1, 0);
		} else if (x == -1 && y == 0) {
			playerModel.transform.setToRotation(0, 1, 0, 90);
			if (moved) updatePlayerModelTransform(1, 0);
			System.out.println(moved);
		}

		if (!moved)
			updatePlayerModelTransform(0, 0);

		playerAnimation.setAnimation("Cube|Movement", 1, 1.0f, this);
		playerAnimating = true;
	}

	public ModelInstance getPlayerModelInstance(TileAttributes.TColor key) {
		switch (key) {
			case RED: return playerRedModel;
			case GREEN: return playerGreenModel;
			case BLUE: return playerBlueModel;
			case YELLOW: return playerYellowModel;
			default: return playerNormalModel;
		}
	}

	private void updatePlayerModelTransform(int x, int y) {
		playerModel.transform.setTranslation(
				-width / 2 + (player.getX() + x) * 10.0f, 7.5f,
				height / 2 - (player.getY() + y) * 10.0f);
	}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		// Update player color.
		if (player.getKey() != oldPlayerKey) {
			playerModel = getPlayerModelInstance(player.getKey());
			oldPlayerKey = player.getKey();
		}
		if (animation.animation.id.equals("Cube|Movement")) {
			playerAnimating = false;
			playerAnimation.setAnimation(null);
			playerAnimation.setAnimation("Cube|Movement", 1, this);
			updatePlayerModelTransform(0, 0);
		}
		// Simplify...
		if (animation.animation.id.equals("Cube|MovementForward")) {
			Quaternion q = new Quaternion();
			playerModel.transform.getRotation(q);
			System.out.println(q.toString());
			Vector3 v = new Vector3();
			playerModel.transform.getTranslation(v);
			System.out.println(v.toString());
			playerAnimating = false;
			playerAnimation.setAnimation(null);
			playerAnimation.setAnimation("Cube|MovementForward", 1, 5.0f, this);
			updatePlayerModelTransform(0, 0);
		} else if (animation.animation.id.equals("Cube|MovementBack")) {
			Quaternion q = new Quaternion();
			playerModel.transform.getRotation(q);
			System.out.println(q.toString());
			Vector3 v = new Vector3();
			playerModel.transform.getTranslation(v);
			System.out.println(v.toString());
			playerAnimating = false;
			playerAnimation.setAnimation(null);
			playerAnimation.setAnimation("Cube|MovementBack", 1, 5.0f, this);
			updatePlayerModelTransform(0, 0);
		} else if (animation.animation.id.equals("Cube|MovementRight")) {
			Quaternion q = new Quaternion();
			playerModel.transform.getRotation(q);
			System.out.println(q.toString());
			Vector3 v = new Vector3();
			playerModel.transform.getTranslation(v);
			System.out.println(v.toString());
			playerAnimating = false;
			playerAnimation.setAnimation(null);
			playerAnimation.setAnimation("Cube|MovementRight", 1, 5.0f, this);
			updatePlayerModelTransform(0, 0);
		} else if (animation.animation.id.equals("Cube|MovementLeft")) {
			Quaternion q = new Quaternion();
			playerModel.transform.getRotation(q);
			System.out.println(q.toString());
			Vector3 v = new Vector3();
			playerModel.transform.getTranslation(v);
			System.out.println(v.toString());
			playerAnimating = false;
			playerAnimation.setAnimation(null);
			playerAnimation.setAnimation("Cube|MovementLeft", 1, 5.0f, this);
			//playerModel.transform.set()
			updatePlayerModelTransform(0, 0);



		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {

	}

	public boolean inAction() {
		return playerAnimating;
	}
}
