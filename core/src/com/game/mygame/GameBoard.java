package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.graphics.g2d.Batch;
import com.badlogic.gdx.graphics.g3d.Environment;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.environment.DirectionalLight;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.badlogic.gdx.math.Vector3;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.actions.DelayAction;

public class GameBoard extends Actor implements AnimationController.AnimationListener {

	// animationcontroller update in draw: nicht thread safe? bessere lösung finden...
	// animationcontroller kann man nicht resetten? null und neu zuweisen geht nicht.
	// player model hat bei fall animation verrutschten origin punkt

	// TODO: sameAnimationAllowed in playerAnimations auf true setzen, dann nicht mehr auf null setzen um zu resetten

	private MyGame game;
	private Player player;
	private OrthographicCamera camera;
	private Environment environment;

	private TileModel[][] modelMatrix;
	private float width, height;

	//private ModelInstance playerModel;
	private PlayerModel playerModel;
	private ModelInstance playerModels[];

	private AnimationController playerAnimation;
	private AnimationController playerAnimations[];
	private BlendAnimation playerBlendAnimation;

	private TileAttributes.TColor oldPlayerKey;
	private boolean playerMoving;
	private boolean resetting;
	private int firstRowRevived;

	public GameBoard(Level level, Player player, OrthographicCamera camera, MyGame game) {
		this.game = game;
		this.player = player;
		this.camera = camera;

		width = (float) level.getColumns() * 10;
		height = (float) level.getRows() * 10;
		modelMatrix = parseMatrix(level.getMatrix());

		environment = new Environment();
		environment.set(new ColorAttribute(ColorAttribute.AmbientLight, .4f, .4f, .4f, 1f));
		environment.add(new DirectionalLight().set(0.8f, 0.8f, 0.8f, -0.5f, -1f, 1f));

		camera.position.set(0, 25.0f, height/2);
		camera.zoom = 0.12f;
		camera.update();

		/*
		playerModels = new ModelInstance[5];
		playerModels[0] = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.NONE));
		playerModels[1] = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.RED));
		playerModels[2] = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.GREEN));
		playerModels[3] = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.BLUE));
		playerModels[4] = new ModelInstance(game.getPlayerModel(
				TileAttributes.TColor.YELLOW));
		playerModel = playerModels[0];
		updatePlayerModelTransform(0, 0);
		*/

		playerModel = new PlayerModel(
				game.getPlayerModel(TileAttributes.TColor.NONE),
				player, -width/2, height/2);

		player.addObserver(playerModel);
		/*
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
		*/

		playerBlendAnimation = new BlendAnimation(playerModel, 1.2f);

		oldPlayerKey = player.getKey();
		playerMoving = false;
		resetting = false;
		firstRowRevived = -1;
	}

	@Override
	public void draw(Batch batch, float parentAlpha) {
		// Batch end, begin necessary?
		batch.end();
		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);
		game.getModelBatch().begin(camera);

		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				if (resetting && firstRowRevived == -1) {
					firstRowRevived = modelMatrix[i][j].getFirstRowRevived(
							firstRowRevived);
				}
				modelMatrix[i][j].update(Gdx.graphics.getDeltaTime());
				if (!modelMatrix[i][j].isDead())
					game.getModelBatch().render(modelMatrix[i][j], environment);
			}
		}
		if (!resetting && firstRowRevived != -1)
			firstRowRevived = -1;

		playerModel.update(Gdx.graphics.getDeltaTime());
		/*
		if (playerMoving) {
			playerAnimation.update(Gdx.graphics.getDeltaTime());
		} else if (resetting) {
			playerAnimation.update(Gdx.graphics.getDeltaTime());
			playerBlendAnimation.update(Gdx.graphics.getDeltaTime());
		}
		*/
		game.getModelBatch().render(playerModel, environment);

		game.getModelBatch().end();
		batch.begin();
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
				//playerModel = getPlayerModelInstance(player.getKey());
				//playerAnimation = getPlayerAnimation(player.getKey());
				oldPlayerKey = player.getKey();
			}

			playerModel.transform.setToRotation(0, 1, 0, 0);
			updatePlayerModelTransform(0, 0);
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (animation.speed > 0.0f) {
				//playerModel.transform.setToRotation(0, 1, 0, 0);
				updatePlayerModelTransform(0, 0.5f);
				playerAnimation.setAnimation(null);
				playerAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
				playerBlendAnimation.reset(0.0f);
			} else {
				//playerAnimation = getPlayerAnimation(
				//		TileAttributes.TColor.NONE);
				resetting = false;
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	public void triggerPlayerMovement(int x, int y, boolean moved) {
		/*
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
		*/
		playerModel.move(x, y, moved);
	}

	public void reset() {
		/*
		Vector3 corrected = new Vector3();
		playerModel.transform.getTranslation(corrected);
		corrected.z -= 5f;

		playerModel = getPlayerModelInstance(TileAttributes.TColor.NONE);
		playerModel.transform.setToRotation(0, 1, 0, 0);
		playerModel.transform.setToTranslation(corrected);

		playerAnimation.setAnimation("Cube|Fall", 1, 1.0f, this);
		oldPlayerKey = TileAttributes.TColor.NONE;
		*/

		playerModel.reset();
		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				modelMatrix[i][j].reset();
			}
		}

		resetting = true;
	}

	private TileModel[][] parseMatrix(Tile[][] matrix) {
		TileModel[][] modelMatrix =
				new TileModel[matrix.length][matrix[0].length];

		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				Model model;
				if (matrix[i][j] instanceof KeyTile) {
					KeyTile keyTile = (KeyTile) matrix[i][j];
					model = game.getLockTileModel(keyTile.getColor());
				} else if (matrix[i][j] instanceof LockTile) {
					LockTile lockTile = (LockTile) matrix[i][j];
					model = game.getLockTileModel(lockTile.getColor());
				} else if (matrix[i][j] instanceof ExitTile) {
					model = game.getExitTileModel();
				} else {
					model = game.getTileModel();
				}
				modelMatrix[i][j] = new TileModel(
						model, matrix[i][j], i, j);
				modelMatrix[i][j].transform.setTranslation(
						-width/2 + j*10.0f, 0.0f, height/2 - i*10.0f);
				matrix[i][j].addObserver(modelMatrix[i][j]);
			}
		}

		return modelMatrix;
	}

	private void updatePlayerModelTransform(float correctX, float correctY) {
		playerModel.transform.setTranslation(
				-width / 2 + (player.getX() + correctX) * 10.0f, 7.5f,
				height / 2 - (player.getY() + correctY) * 10.0f);
	}

	/*
	private ModelInstance getPlayerModelInstance(TileAttributes.TColor key) {
		switch (key) {
			case RED:		return playerModels[1];
			case GREEN:		return playerModels[2];
			case BLUE:		return playerModels[3];
			case YELLOW:	return playerModels[4];
			default:		return playerModels[0];
		}
	}

	private AnimationController getPlayerAnimation(TileAttributes.TColor key) {
		switch (key) {
			case RED:		return playerAnimations[1];
			case GREEN:		return playerAnimations[2];
			case BLUE:		return playerAnimations[3];
			case YELLOW:	return playerAnimations[4];
			default:		return playerAnimations[0];
		}
	}
	*/

	public boolean isPlayerMoving() {
		return playerMoving;
	}

}

