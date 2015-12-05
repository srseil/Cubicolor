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

public class GameBoard extends Actor {

	// animationcontroller update in draw: nicht thread safe? bessere lösung finden...
	// animationcontroller kann man nicht resetten? null und neu zuweisen geht nicht.
	// ---> geht schon, aber unnötig. einfach reset methode?
	// player model hat bei fall animation verrutschten origin punkt

	private MyGame game;
	private OrthographicCamera camera;
	private Environment environment;

	private TileModel[][] modelMatrix;
	private float width, height;
	private ExitTileModel exitModel;
	private PlayerModel playerModel;

	private int firstRowRevived;
	private boolean resetting;

	public GameBoard(Level level, Player player, OrthographicCamera camera, MyGame game) {
		this.game = game;
		this.camera = camera;

		width = (float) level.getColumns() * 10;
		height = (float) level.getRows() * 10;
		modelMatrix = parseMatrix(level.getMatrix());

		playerModel = new PlayerModel(
				game.getPlayerModel(TileAttributes.TColor.NONE),
				player, -width/2, height/2);
		player.addObserver(playerModel);

		environment = new Environment();
		environment.set(new ColorAttribute(ColorAttribute.AmbientLight, .4f, .4f, .4f, 1f));
		environment.add(new DirectionalLight().set(0.8f, 0.8f, 0.8f, -0.5f, -1f, 1f));

		camera.position.set(0, 25.0f, height/2);
		camera.zoom = 0.12f;
		camera.update();

		resetting = false;
		firstRowRevived = -1;
	}

	private TileModel[][] parseMatrix(Tile[][] matrix) {
		TileModel[][] modelMatrix =
				new TileModel[matrix.length][matrix[0].length];

		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				Model model;
				if (matrix[i][j] instanceof ExitTile) {
					ExitTile exitTile = (ExitTile) matrix[i][j];
					exitModel = new ExitTileModel(game.getExitTileModel(),
							exitTile, -width/2 + j*10.0f, height/2 - i*10.0f);
					exitTile.addObserver(exitModel);
					continue;
				}
				else if (matrix[i][j] instanceof KeyTile) {
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

	@Override
	public void draw(Batch batch, float parentAlpha) {
		// Batch end, begin necessary?
		batch.end();
		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);
		game.getModelBatch().begin(camera);

		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				if (modelMatrix[i][j] == null)
					continue;
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

		exitModel.update(Gdx.graphics.getDeltaTime());
		game.getModelBatch().render(exitModel, environment);

		playerModel.update(Gdx.graphics.getDeltaTime());
		game.getModelBatch().render(playerModel, environment);

		game.getModelBatch().end();
		batch.begin();
	}

	public void reset() {
		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				if (modelMatrix[i][j] != null)
					modelMatrix[i][j].reset();
			}
		}
		exitModel.reset();
		playerModel.reset();
		resetting = true;
	}

	public void triggerPlayerMovement(int x, int y, boolean moved) {
		playerModel.move(x, y, moved);
	}

	public boolean isPlayerMoving() {
		return playerModel.isMoving();
	}

}

