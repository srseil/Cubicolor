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
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.actions.DelayAction;

public class GameBoard extends Actor implements AnimationController.AnimationListener {

	// animationcontroller update in draw: nicht thread safe? bessere lösung finden...
	// animationcontroller kann man nicht resetten? null und neu zuweisen geht nicht.


	// TODO: sameAnimationAllowed in playerAnimations auf true setzen, dann nicht mehr auf null setzen um zu resetten

	private MyGame game;
	//private Level level;
	private Tile[][] matrix;
	private Player player;
	private OrthographicCamera camera;
	private Environment environment;

	//private ModelInstance[][] tileMatrix;
	private TileModel[][] tileMatrix;
	private float width, height;

	private ModelInstance playerModel;
	private ModelInstance playerModels[];

	private AnimationController playerAnimation;
	private AnimationController playerAnimations[];

	private AnimationController tileAnimations[][];
	private BlendAnimation tileBlendAnimations[][];

	private TileAttributes.TColor oldPlayerKey;
	private boolean playerMoving;
	private float reviveDelta;

	private int firstRowRevived;

	BlendAnimation blendAnimation;
	DelayAction da;


	public GameBoard(Level level, Player player, OrthographicCamera camera, MyGame game) {
		this.game = game;
		this.matrix = level.getMatrix();
		this.player = player;
		this.camera = camera;

		width = (float) level.getColumns() * 10;
		height = (float) level.getRows() * 10;
		tileMatrix = parseMatrix(matrix);

		camera.position.set(0, 25.0f, height/2);
		camera.zoom = 0.12f;
		camera.update();

		environment = new Environment();
		environment.set(new ColorAttribute(ColorAttribute.AmbientLight, .4f, .4f, .4f, 1f));
		environment.add(new DirectionalLight().set(0.8f, 0.8f, 0.8f, -0.5f, -1f, 1f));

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

		tileAnimations
				= new AnimationController[matrix.length][matrix[0].length];
		for (int i = 0; i < tileAnimations.length; i++) {
			for (int j = 0; j < tileAnimations[i].length; j++) {
				tileAnimations[i][j] =
						new AnimationController(tileMatrix[i][j]);
				tileAnimations[i][j].allowSameAnimation = true;
				tileAnimations[i][j].setAnimation("Cube|Fall", 1, 1f, null);
			}
		}

		tileBlendAnimations =
				new BlendAnimation[matrix.length][matrix[0].length];
		for (int i = 0; i < tileAnimations.length; i++) {
			for (int j = 0; j < tileAnimations[i].length; j++) {
				tileBlendAnimations[i][j] =	new BlendAnimation(tileMatrix[i][j],
								tileAnimations[i][j].current.duration);
			}
		}

		/*
		playerAnimation.setAnimation("Cube|Movement");
		System.out.println(playerAnimation.current.duration);
		for (int i = 0; i < 100; i++) {
			System.out.println(game.getPlayerModel(TileAttributes.TColor.NONE).nodes.get(i).id);
		}
		*/
		blendAnimation = new BlendAnimation(playerModel, 1f);
		da = new DelayAction(1.0f);

		oldPlayerKey = player.getKey();
		playerMoving = false;
		reviveDelta = 0.0f;

		firstRowRevived = -1;
	}

	@Override
	public void draw(Batch batch, float parentAlpha) {
		// Batch end, begin necessary?
		batch.end();
		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);
		game.getModelBatch().begin(camera);

		//System.out.println(Gdx.graphics.getDeltaTime());

		for (int i = 0; i < tileMatrix.length; i++) {
			for (int j = 0; j < tileMatrix[i].length; j++) {
				firstRowRevived = tileMatrix[i][j].update(
						Gdx.graphics.getDeltaTime(), firstRowRevived);
				if (!tileMatrix[i][j].isDead())
					game.getModelBatch().render(tileMatrix[i][j], environment);
			}
		}

		// necessary?
		if (firstRowRevived != -1)
			firstRowRevived = -1;

		/*
		for (int i = 0; i < matrix.length; i++) {
			for (int j = 0; j < matrix[i].length; j++) {
				// Update dying animations (blending and moving).
				if (matrix[i][j].isDead() && !matrix[i][j].isReviving()) {
					continue;
				} else if (matrix[i][j].isDying()) {
					tileBlendAnimations[i][j].update(
							Gdx.graphics.getDeltaTime());
					tileAnimations[i][j].update(Gdx.graphics.getDeltaTime());
					if (!tileBlendAnimations[i][j].inAction()) {
						matrix[i][j].setDying(false);
						matrix[i][j].setDead(true);
						System.out.println("killed");
					}
				} else if (matrix[i][j].isReviving()) {
					reviveDelta += Gdx.graphics.getDeltaTime();

					if (firstRowRevived == -1)
						firstRowRevived = i;


					if (reviveDelta >= (firstRowRevived - i)*1.0f + j*0.5f) {
						tileBlendAnimations[i][j].update(-1.0f *
								Gdx.graphics.getDeltaTime());
						System.out.println("revive...");
						tileAnimations[i][j].update(Gdx.graphics.getDeltaTime());
						if (!tileBlendAnimations[i][j].inAction()) {
						//if (!tileAnimations[i][j].inAction) {
							System.out.println("in");
							matrix[i][j].setReviving(false);
							matrix[i][j].setDead(false);
							tileBlendAnimations[i][j].reset(1.0f);
							tileAnimations[i][j].setAnimation("Cube|Fall");
							if (i == matrix.length-1 && j == matrix[i].length-1)
								firstRowRevived = -1;
						}
					}
				}
				game.getModelBatch().render(tileMatrix[i][j], environment);
			}
		}
		*/

		if (playerMoving) {
			playerAnimation.update(Gdx.graphics.getDeltaTime());
		}

		//blendAnimation.update(Gdx.graphics.getDeltaTime());
		//tileBlendAnimations[0][2].update(Gdx.graphics.getDeltaTime());

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

	public void reset() {
		playerModel = getPlayerModelInstance(TileAttributes.TColor.NONE);
		playerAnimation = getPlayerAnimation(TileAttributes.TColor.NONE);
		oldPlayerKey = TileAttributes.TColor.NONE;
		playerModel.transform.setToRotation(0, 1, 0, 0);
		updatePlayerModelTransform(0, 0);

		for (int i = 0; i < tileMatrix.length; i++) {
			for (int j = 0; j < tileMatrix[i].length; j++) {
				tileMatrix[i][j].reset();
			}
		}
		/*
		for (int i = 0; i < tileAnimations.length; i++) {
			for (int j = 0; j < tileAnimations[i].length; j++) {
				//tileAnimations[i][j].setAnimation(null);
				if (matrix[i][j].isReviving()) {
					tileAnimations[i][j].setAnimation("Cube|Fall", 1, -1.0f, null);
				} else {
					tileAnimations[i][j].setAnimation("Cube|Fall", 1, 1.0f, null);
				}
				tileAnimations[i][j].update(Gdx.graphics.getDeltaTime());
			}
		}

		for (int i = 0; i < tileBlendAnimations.length; i++) {
			for (int j = 0; j < tileBlendAnimations[i].length; j++) {
				if (matrix[i][j].isReviving())
					tileBlendAnimations[i][j].reset(0.0f);
			}
		}


		reviveDelta = 0.0f;
		*/
	}

	private TileModel[][] parseMatrix(Tile[][] tileMatrix) {
		TileModel[][] modelMatrix =
				new TileModel[tileMatrix.length][tileMatrix[0].length];

		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				Model model;
				if (tileMatrix[i][j] instanceof KeyTile) {
					KeyTile keyTile = (KeyTile) tileMatrix[i][j];
					model = game.getLockTileModel(keyTile.getColor());
				} else if (tileMatrix[i][j] instanceof LockTile) {
					LockTile lockTile = (LockTile) tileMatrix[i][j];
					model = game.getLockTileModel(lockTile.getColor());
				} else if (tileMatrix[i][j] instanceof ExitTile) {
					model = game.getExitTileModel();
				} else {
					model = game.getTileModel();
				}
				modelMatrix[i][j] = new TileModel(
						model, tileMatrix[i][j], i, j);
				modelMatrix[i][j].transform.setTranslation(
						-width/2 + j*10.0f, 0.0f, height/2 - i*10.0f);
				tileMatrix[i][j].addObserver(modelMatrix[i][j]);
			}
		}

		/*
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
		*/

		return modelMatrix;
	}

	private void updatePlayerModelTransform(int x, int y) {
		playerModel.transform.setTranslation(
				-width / 2 + (player.getX() + x) * 10.0f, 7.5f,
				height / 2 - (player.getY() + y) * 10.0f);
	}

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

	public boolean isPlayerMoving() {
		return playerMoving;
	}

}
