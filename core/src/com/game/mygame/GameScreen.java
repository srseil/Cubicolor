package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.*;
import com.badlogic.gdx.graphics.g3d.Environment;
import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.environment.DirectionalLight;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.utils.viewport.ExtendViewport;
import com.badlogic.gdx.utils.viewport.ScreenViewport;
import com.badlogic.gdx.utils.viewport.Viewport;

public class GameScreen implements Screen {

	final MyGame game;

	private GameBoard gameBoard;

	private Stage stage;
	private Table rootTable;
	private Label steps;
	private PauseDialog pauseDialog;
	private WinDialog completeDialogNormal;
	private WinDialog completeDialogOptimal;

	private Level level;
	private Player player;
	private OrthographicCamera camera;
	//private PerspectiveCamera pcam;
	private OrthographicCamera pcam;

	private boolean paused;
	private boolean pauseClosed;
	private boolean completed;
	// !!! -> eher solved als completed?

	public GameScreen(Level level, OrthographicCamera camera, final MyGame game) {
		this.level = level;
		this.camera = camera;
		this.game = game;

		//--
		//pcam = new PerspectiveCamera(67, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		pcam = new OrthographicCamera(Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		//pcam.lookAt(0, 0, 0);
		pcam.rotate(-60.0f, 1.0f, 0.0f, 0.0f);
		pcam.rotate(20.0f, 0.0f, 1.0f, 0.0f);
		//pcam.zoom = 0.06f;
		pcam.near = 1f;
		pcam.far = 300f;
		pcam.update();
		//--

		player = new Player(level, this);

		//camera.setToOrtho(false, 800, 600);
		stage = new Stage(new ExtendViewport(800, 600));
		//Gdx.input.setInputProcessor(stage);

		gameBoard = new GameBoard(level, player, pcam, game, game.getShapeRenderer());

		rootTable = new Table();
		rootTable.setFillParent(true);
		rootTable.setDebug(true);
		stage.addActor(rootTable);

		Skin skin = new Skin(Gdx.files.internal("uiskin.json"));

		Table leftUI = new Table();
		Table boardTable = new Table();
		Table rightUI = new Table();
		rootTable.add(leftUI).top().left();
		rootTable.add(boardTable).expand().center();
		rootTable.add(rightUI).bottom().right();

		Label message = new Label("Message", skin);
		steps = new Label("number of steps: ", skin);

		leftUI.add(message);
		rightUI.add(steps).width(160.0f);

		//Label test = new Label("Test", skin);
		//Image board = new Image(gameBoard);
		//boardTable.add(board);
		//boardTable.add(test);

		boardTable.add(gameBoard);

		//table.add(steps);
		//table.bottom().right();
		pauseDialog = new PauseDialog(skin, this, game);

		completeDialogNormal = new WinDialog(false, skin, this, game);
		completeDialogOptimal = new WinDialog(true, skin, this, game);
	}

	public void process() {
		if (pauseClosed) {
			pauseClosed = false;
			return;
		}

		if (!paused && !completed) {
			if (Gdx.input.isKeyJustPressed(Input.Keys.UP)) {
				player.move(0, 1);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.DOWN)) {
				player.move(0, -1);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.LEFT)) {
				player.move(-1, 0);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.RIGHT)) {
				player.move(1, 0);
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.R)) {
				resetLevel();
			} else if (Gdx.input.isKeyJustPressed(Input.Keys.ESCAPE)) {
				paused = true;
				pauseDialog.show(stage);
			}
		}
	}

	public void resetLevel() {
		level.reset();
		player.reset();
		completed = false;
	}

	public void completeLevel(boolean optimal) {
		completed = true;
		completeDialogNormal.show(stage);
		game.getSaveState().update(
				level.getDifficulty(), level.getNumber(), optimal);

		//game.getSaveState().save();

		System.out.println(game.getSaveState().getSolveState(level.getDifficulty(), level.getNumber()));
	}

	// The render() method is being used as a hook into the game loop.
	@Override
	public void render(float delta) {
		/*
		camera.update();
		game.getShapeRenderer().setProjectionMatrix(camera.combined);
		game.getSpriteBatch().setProjectionMatrix(camera.combined);

		// Set background color.
		Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT);

		game.getShapeRenderer().begin(ShapeRenderer.ShapeType.Filled);


		steps.setText("number of steps: " + player.getSteps());
		*/

		/*
		game.getSpriteBatch().begin();
		game.getFont().draw(game.getSpriteBatch(), "This is the game screen!", 10, 780);
		game.getFont().draw(game.getSpriteBatch(),
				Integer.toString(player.getSteps()), 770, 20);
		game.getSpriteBatch().end();
		*/


		// Set background color.
		Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);

		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);
		/*
		game.getModelBatch().begin(pcam);
		game.getModelBatch().render(modelInstance, environment);
		game.getModelBatch().end();
		*/

		steps.setText("number of steps: " + player.getSteps());

		stage.getBatch().setProjectionMatrix(pcam.combined);
		stage.act(delta);
		stage.draw();


		/*
		Tile[][] matrix = level.getMatrix();

		// Render game board.
		for (int i = 0; i < level.getRows(); i++) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!matrix[i][j].isDead())
					matrix[i][j].draw(200f + j*50f, 100f + i*50f, game.getShapeRenderer());
			}
		}
		*/

		// Render player.

		/*
		game.getShapeRenderer().end();

		*/

		if (!completed && player.hasCompleted()) {
			completed = true;
			completeDialogOptimal.show(stage);
		}

		// Process game logic and input.
		process();
	}

	@Override
	public void show() {
		//stage.getViewport().setCamera(camera);
		//camera.update();
		//stage.getViewport().setCamera(pcam);
		//pcam.update();
		// Stage hat eigene Kamera?
		Gdx.input.setInputProcessor(stage);
	}

	@Override
	public void dispose() {
		stage.dispose();
	}

	@Override
	public void resize(int width, int height) {
		stage.getViewport().update(width, height, true);
	}

	@Override
	public void hide() {}

	@Override
	public void pause() {}

	@Override
	public void resume() {}

	public void setPaused(boolean paused) {
		this.paused = paused;
	}

	public void setPauseClosed(boolean pauseClosed) {
		this.pauseClosed = pauseClosed;
	}

}
