package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.Camera;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.graphics.g2d.Batch;
import com.badlogic.gdx.graphics.g3d.Environment;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.environment.DirectionalLight;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.utils.Timer;

public class GameBoard extends Actor {

	private MyGame game;
	private OrthographicCamera camera;
	private Environment environment;
	private Level level;
	private TileModel[][] modelMatrix;
	private TileModel lastTile;
	private ExitTileModel exitModel;
	private PlayerModel playerModel;

	public GameBoard(
			Level level, Player player, Camera stageCamera, MyGame game) {
		this.game = game;
		this.camera = (OrthographicCamera) stageCamera;
		this.level = level;

		float width = (float) level.getColumns() * TileModel.SIZE;
		float height = (float) level.getRows() * TileModel.SIZE;
		modelMatrix = parseModelMatrix(level.getMatrix());
		lastTile = calculateLastRevivingTile();

		playerModel = new PlayerModel(
				game.getPlayerModel(), player, exitModel, lastTile, this, game);

		environment = new Environment();
		environment.set(new ColorAttribute(
				ColorAttribute.AmbientLight, .6f, .6f, .6f, 1f));
		DirectionalLight directionalLight = new DirectionalLight();
		directionalLight.setDirection(-0.15f, -1.0f, -0.6f);
		directionalLight.setColor(0.3f, 0.3f, 0.3f, 1.0f);
		environment.add(directionalLight);

		// Adjust the camera's position and angle for the corresponding level.
		float correctY = 0.0f;
		if (exitModel.getRow() == level.getRows() - 1
				&& exitModel.getColumn() <= level.getRequirementsNumber()) {
			// Exit tile is in upper row.
			// Tweak this if some levels are too high/low.
			correctY = 0.5f * (level.getRequirementsNumber() + 1
					- exitModel.getColumn() * 1.75f);
			correctY = Math.max(correctY, 0.0f);
		} else if (exitModel.getRow() == level.getRows() - 2
				&& exitModel.getColumn() == 0) {
			// Exit tile is in second upper row on the far left.
			correctY = 0.5f * Math.max(0, (level.getRequirementsNumber() - 2));
		}
		float viewportHeight = (float) (Math.cos(Math.toRadians(35.0f)) *
				(Math.sqrt(Math.pow(2 * level.getRows(), 2) +
						Math.pow(2 * level.getColumns(), 2))) + 1);
		float viewportWidth = ((float) Gdx.graphics.getWidth())
				/ Gdx.graphics.getHeight() * viewportHeight;



		// posX = horizontal width/2 of board in camera angle
		float posX = (float) (Math.cos(Math.toRadians(20.0f))
				* Math.sqrt(Math.pow(level.getRows(), 2.0f)
				+ Math.pow(level.getColumns(), 2.0f)));
		float r = posX / (float) Math.cos(Math.toRadians(20.0f));
		float b = (float) Math.sin(Math.toRadians(20.0f)) * posX;



		camera.setToOrtho(false, viewportWidth, viewportHeight);
		camera.rotate(-60.0f, 1.0f, 0.0f, 0.0f);
		camera.rotate(20.0f, 0.0f, 1.0f, 0.0f);
		camera.near = 1.0f;
		camera.far = 30.0f;
		System.out.println("posX: " + posX + ", r: " + r + ", width/2: " + width/2);
		camera.position.set(width/2, level.getRows() * 1.5f + correctY, 0.0f);
		//camera.zoom = 1.2f;
		camera.update();


		if (exitModel.getRow() == level.getRows() - 1
				&& exitModel.getColumn() <= level.getRequirementsNumber()) {
			// Exit tile is in upper row.
			// Tweak this if some levels are too high/low.
			correctY = 0.5f * (level.getRequirementsNumber()
					- exitModel.getColumn() * 1.5f);
			if (level.getDifficulty() == Difficulty.SMART)
				correctY -= 0.6f;
			if (level.getDifficulty() == Difficulty.GENIUS && level.getNumber() == 4)
				correctY -= 0.3f;
			correctY = Math.max(correctY, 0.0f);
		} else if (exitModel.getRow() == level.getRows() - 2
				&& exitModel.getColumn() == 0) {
			// Exit tile is in second upper row on the far left.
			correctY = 0.5f * Math.max(0, (level.getRequirementsNumber() - 3));
		}
		camera.position.set(width/2, 10.0f + correctY*0, -height/2 + 6.6f - correctY);
		//camera.position.set(posX, 10.0f + correctY*0, -height/2 + 6.6f - correctY);
		camera.zoom = 1.2f;
		camera.update();

		setup();
	}

	private TileModel[][] parseModelMatrix(Tile[][] matrix) {
		TileModel[][] modelMatrix =
				new TileModel[matrix.length][matrix[0].length];

		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = 0; j < modelMatrix[i].length; j++) {
				Model model;
				if (matrix[i][j] instanceof EmptyTile) {
					// Empty tile, entry is null.
					continue;
				}  else if (matrix[i][j] instanceof ExitTile) {
					// Exit tile model, entry is null.
					ExitTile exitTile = (ExitTile) matrix[i][j];
					exitModel = new ExitTileModel(
							game.getExitTileModel(), exitTile,
							level.getExitRequirements(),
							j * TileModel.SIZE, -i * TileModel.SIZE,
							i, j, level, game);
					exitTile.addObserver(exitModel);
					continue;
				} else if (matrix[i][j] instanceof KeyTile) {
					// Key tile model
					KeyTile keyTile = (KeyTile) matrix[i][j];
					model = game.getKeyTileModel();
					modelMatrix[i][j] = new KeyTileModel(
							model, keyTile, i, j, level, game);
				} else if (matrix[i][j] instanceof LockTile) {
					// Lock tile model
					LockTile lockTile = (LockTile) matrix[i][j];
					model = game.getLockTileModel(lockTile.getLockColor());
					modelMatrix[i][j] = new TileModel(
							model, matrix[i][j], i, j, level, game);
				} else {
					// Tile model
					model = game.getTileModel();
					modelMatrix[i][j] = new TileModel(
							model, matrix[i][j], i, j, level, game);
				}
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

	public TileModel calculateLastRevivingTile() {
		TileModel lastTile =
				modelMatrix[level.getStartRow()][level.getStartColumn()];
		for (int i = 0; i < modelMatrix.length; i++) {
			for (int j = modelMatrix[i].length - 1; j >= 0; j--) {
				if (modelMatrix[i][j] != null
						&& modelMatrix[i][j].isReviving()) {
					lastTile = modelMatrix[i][j];
					break;
				}
			}
			if (lastTile != null)
				break;
		}
		return lastTile;
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

		for (RequirementModel reqModel : exitModel.getRequirementModels()) {
			reqModel.update(Gdx.graphics.getDeltaTime());
			game.getModelBatch().render(reqModel, environment);
		}

		exitModel.update(Gdx.graphics.getDeltaTime());
		game.getModelBatch().render(exitModel, environment);

		playerModel.update(Gdx.graphics.getDeltaTime());
		game.getModelBatch().render(playerModel, environment);

		game.getModelBatch().end();
		batch.begin();
	}

	public void reset() {
		lastTile = calculateLastRevivingTile();
		resetModelMatrix();
		exitModel.reset();
		playerModel.reset();
	}

	public void setup() {
		resetModelMatrix();
		exitModel.setup();

		// Delay player setup until board has finished setting up.
		TileModel startTile =
				modelMatrix[level.getStartRow()][level.getStartColumn()];
		Timer.schedule(new Timer.Task() {
			@Override
			public void run() {
				playerModel.setup();
			}
		}, lastTile.getReviveDelay() + 0.5f);
		// Old delay: (modelMatrix.length + 2) * TileModel.ROW_REVIVE_DELAY)
	}

	public void movePlayerModel(int dx, int dy) {
		playerModel.move(dx, dy);
	}

	public boolean isControllable() {
		return playerModel.isControllable();
	}

	public boolean isOccupied() {
		return playerModel.isOccupied();
	}

	public boolean isCompleted() {
		return playerModel.hasCompleted();
	}

}

