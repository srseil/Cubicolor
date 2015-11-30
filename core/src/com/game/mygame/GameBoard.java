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

	private MyGame game;
	private Level level;
	private Player player;
	private Environment environment;
	private ModelInstance[][] matrix;
	private float width, height;

	private ModelInstance playerModel;
	private ModelInstance playerNormalModel;
	private ModelInstance playerRedModel;
	private ModelInstance playerGreenModel;
	private ModelInstance playerBlueModel;
	private ModelInstance playerYellowModel;

	private AnimationController playerAnimation;
	private AnimationController playerAnimations[];

	private TileAttributes.TColor oldPlayerKey;

	private boolean playerMoving;

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

		playerAnimation = new AnimationController(playerModel);
		//animationController.setAnimation("Cube|CubeMovement", -1, 2.0f, null);


		playerAnimations = new AnimationController[5];
		playerAnimations[0] = new AnimationController(
				getPlayerModelInstance(TileAttributes.TColor.NONE));
		playerAnimations[1] = new AnimationController(
				getPlayerModelInstance(TileAttributes.TColor.RED));
		playerAnimations[2] = new AnimationController(
				getPlayerModelInstance(TileAttributes.TColor.GREEN));
		playerAnimations[3] = new AnimationController(
				getPlayerModelInstance(TileAttributes.TColor.BLUE));
		playerAnimations[4] = new AnimationController(
				getPlayerModelInstance(TileAttributes.TColor.YELLOW));

		playerAnimation = playerAnimations[0];


		playerMoving = false;
		//playerModel.transform.setToTranslation(-20.0f, 7.5f, 15.0f);
		//playerModel.transform.setToRotation(0, 1, 0, 90);
		playerModel.transform.setTranslation(
				-width / 2 + player.getX() * 10.0f, 7.5f,
				height / 2 - player.getY() * 10.0f);
		//playerAnimation.setAnimation("Cube|Movement", -1, this);
	}

	@Override
	public void draw(Batch batch, float parentAlpha) {
		// Batch end, begin necessary?
		batch.end();
		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);
		game.getModelBatch().begin(camera);

		for (int i = 0; i < level.getRows(); i++) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!level.getMatrix()[i][j].isDead()) {
					game.getModelBatch().render(matrix[i][j], environment);
				}
			}
		}

		if (playerMoving) {
			playerAnimation.update(Gdx.graphics.getDeltaTime());
		}

		game.getModelBatch().render(playerModel, environment);

		game.getModelBatch().end();
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

	public void reset() {
		playerModel = getPlayerModelInstance(TileAttributes.TColor.NONE);
		playerAnimation = getPlayerAnimation(TileAttributes.TColor.NONE);
		oldPlayerKey = TileAttributes.TColor.NONE;
		playerModel.transform.setToRotation(0, 1, 0, 0);
		updatePlayerModelTransform(0, 0);
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
		}

		if (!moved)
			updatePlayerModelTransform(0, 0);

		playerAnimation.setAnimation("Cube|Movement", 1, 3.0f, this);
		playerMoving = true;
	}

	public ModelInstance getPlayerModelInstance(TileAttributes.TColor key) {
		switch (key) {
			case RED:		return playerRedModel;
			case GREEN:		return playerGreenModel;
			case BLUE:		return playerBlueModel;
			case YELLOW:	return playerYellowModel;
			default:		return playerNormalModel;
		}
	}

	public AnimationController getPlayerAnimation(TileAttributes.TColor key) {
		switch (key) {
			case RED:		return playerAnimations[1];
			case GREEN:		return playerAnimations[2];
			case BLUE:		return playerAnimations[3];
			case YELLOW:	return playerAnimations[4];
			default:		return playerAnimations[0];
		}
	}

	private void updatePlayerModelTransform(int x, int y) {
		playerModel.transform.setTranslation(
				-width / 2 + (player.getX() + x) * 10.0f, 7.5f,
				height / 2 - (player.getY() + y) * 10.0f);
	}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Movement")) {
			playerMoving = false;

			// Reset animation.
			playerAnimation.setAnimation(null);
			playerAnimation.setAnimation("Cube|Movement", 1, 3.0f, this);

			// Update player model if key has been taken.
			if (player.getKey() != oldPlayerKey) {
				playerModel = getPlayerModelInstance(player.getKey());
				playerAnimation = getPlayerAnimation(player.getKey());
				oldPlayerKey = player.getKey();
			}

			/*
			Vector3 v = new Vector3();
			Quaternion q = new Quaternion();
			playerModel.transform.getRotation(q);
			playerModel.transform.getTranslation(v);
			System.out.println("before: " + v + " " + q);
			*/
			playerModel.transform.setToRotation(0, 1, 0, 0);
			updatePlayerModelTransform(0, 0);
			/*
			playerModel.transform.getRotation(q);
			playerModel.transform.getTranslation(v);
			System.out.println("after: " + v + " " + q);
			*/
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	public boolean isPlayerMoving() {
		return playerMoving;
	}

}
