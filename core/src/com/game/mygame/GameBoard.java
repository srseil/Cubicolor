package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.graphics.g2d.Batch;
import com.badlogic.gdx.graphics.g3d.Environment;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.environment.DirectionalLight;
import com.badlogic.gdx.scenes.scene2d.Actor;

public class GameBoard extends Actor {

	// animationcontroller update in draw: nicht thread safe? bessere lösung finden...
	// animationcontroller kann man nicht resetten? null und neu zuweisen geht nicht.
	// ---> geht schon, aber unnötig. einfach reset methode?
	// player model hat bei fall animation verrutschten origin punkt
	// fbx-conv g3dj lädt nicht, fehler mit skalierung

	private MyGame game;
	private OrthographicCamera camera;
	private Environment environment;
	private float width, height;
	private TileModel[][] modelMatrix;
	private ExitTileModel exitModel;
	private PlayerModel playerModel;
	private boolean resetting;

	public GameBoard(Level level, Player player, MyGame game) {
		this.game = game;

		width = (float) level.getColumns() * TileModel.SIZE;
		height = (float) level.getRows() * TileModel.SIZE;
		modelMatrix = parseModelMatrix(level.getMatrix());

		playerModel = new PlayerModel(
				game.getPlayerModel(TileColor.NONE),
				player, -width/2, height/2, modelMatrix, exitModel);
		player.addObserver(playerModel);

		environment = new Environment();
		environment.set(new ColorAttribute(ColorAttribute.AmbientLight, .6f, .6f, .6f, 1f));
		//environment.add(new DirectionalLight().set(0.6f, 0.6f, 0.6f, -0.3f, -1f, 1f));
		DirectionalLight directionalLight = new DirectionalLight();
		directionalLight.setDirection(-0.15f, -1.0f, -0.6f);
		directionalLight.setColor(0.3f, 0.3f, 0.3f, 1.0f);
		environment.add(directionalLight);

		float viewportHeight = (float) (Math.cos(Math.toRadians(35.0f)) *
				(Math.sqrt(Math.pow(2 * level.getRows(), 2) +
						Math.pow(2 * level.getColumns(), 2))) + 1);
		float viewportWidth = (4.0f/3.0f * viewportHeight);
		System.out.println("VIEWPORT_HEIGHT: " + viewportHeight);
		System.out.println("VIEWPORT_WIDTH: " + viewportWidth);
		camera = new OrthographicCamera(viewportWidth, viewportHeight);
		camera.rotate(-60.0f, 1.0f, 0.0f, 0.0f);
		camera.rotate(20.0f, 0.0f, 1.0f, 0.0f);
		camera.near = 1.0f;
		camera.far = 20.0f;
		/*
		float h = 120.0f;
		float a = (float) ((-height) / Math.cos(Math.toRadians(20)) / 2)
				+ (float) (h * Math.tan(Math.toRadians(60)) + 0.0f);
		a = -viewportHeight/4;// + (float) (10.0f * Math.tan(Math.toRadians(60.0f)));
		*/
		camera.position.set(width/2, 10.0f, 0.5f);
		camera.zoom = 1.2f;
		camera.update();

		resetting = true;
		setup();
	}

	private TileModel[][] parseModelMatrix(Tile[][] matrix) {
		TileModel[][] modelMatrix =
				new TileModel[matrix.length][matrix[0].length];

		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				Model model;
				if (matrix[i][j] instanceof ExitTile) {
					ExitTile exitTile = (ExitTile) matrix[i][j];
					exitModel = new ExitTileModel(
							game.getExitTileModel(), exitTile,
							j * TileModel.SIZE,
							-i * TileModel.SIZE, i, j);
					exitTile.addObserver(exitModel);
					continue;
				} else if (matrix[i][j] instanceof KeyTile) {
					KeyTile keyTile = (KeyTile) matrix[i][j];
					model = game.getKeyTileModel(keyTile.getKeyColor());
				} else if (matrix[i][j] instanceof LockTile) {
					LockTile lockTile = (LockTile) matrix[i][j];
					model = game.getLockTileModel(lockTile.getLockColor());
				} else {
					model = game.getTileModel();
				}
				modelMatrix[i][j] = new TileModel(
						model, matrix[i][j], i, j);
				modelMatrix[i][j].transform.setTranslation(
						j * TileModel.SIZE, 0.0f,
						-i * TileModel.SIZE);
				matrix[i][j].addObserver(modelMatrix[i][j]);
			}
		}

		return modelMatrix;
	}

	private void resetModelMatrix() {
		int firstRowRevived = -1;
		for (int i = modelMatrix.length - 1; i >= 0; i--) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				if (modelMatrix[i][j] != null) {
					modelMatrix[i][j].reset();
					firstRowRevived = modelMatrix[i][j].calculateReviveDelay(
							firstRowRevived);
				}
			}
		}
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
				modelMatrix[i][j].update(Gdx.graphics.getDeltaTime());
				if (!modelMatrix[i][j].isDead())
					game.getModelBatch().render(modelMatrix[i][j], environment);
			}
		}

		exitModel.update(Gdx.graphics.getDeltaTime());
		game.getModelBatch().render(exitModel, environment);

		playerModel.update(Gdx.graphics.getDeltaTime());
		game.getModelBatch().render(playerModel, environment);

		game.getModelBatch().end();
		batch.begin();
	}

	public void reset() {
		resetModelMatrix();
		exitModel.reset();
		playerModel.reset();
	}

	public void setup() {
		resetModelMatrix();
		exitModel.setup();
		playerModel.setup();
	}

	public void movePlayerModel(int dx, int dy) {
		playerModel.move(dx, dy);
	}

	public boolean isControllable() {
		return playerModel.isControllable();
	}

	public boolean isCompleted() {
		return playerModel.hasCompleted();
	}

}

