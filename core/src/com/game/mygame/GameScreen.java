package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.OrthographicCamera;
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
	private WinDialog winDialogNormal;
	private WinDialog winDialogOptimal;

	private Level level;
	private Player player;
	private OrthographicCamera camera;

	private boolean paused;
	private boolean pauseClosed;
	private boolean won;

	public GameScreen(Level level, Player player, OrthographicCamera camera, final MyGame game) {
		this.level = level;
		this.player = player;
		this.game = game;

		this.camera = camera;
		//camera.setToOrtho(false, 800, 600);
		stage = new Stage(new ExtendViewport(800, 600));
		//Gdx.input.setInputProcessor(stage);

		gameBoard = new GameBoard(level, player, game.getShapeRenderer());

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

		winDialogNormal = new WinDialog(false, skin, this, game);
		winDialogOptimal = new WinDialog(true, skin, this, game);
	}

	public void process() {
		if (pauseClosed) {
			pauseClosed = false;
			return;
		}

		if (!paused && !won) {
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
		won = false;
	}

	public void win() {
		won = true;
		winDialogNormal.show(stage);
	}

	// The render() method is being used as a hook into the game loop.
	@Override
	public void render(float delta) {
		camera.update();
		game.getShapeRenderer().setProjectionMatrix(camera.combined);
		game.getSpriteBatch().setProjectionMatrix(camera.combined);

		// Set background color.
		Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT);

		game.getShapeRenderer().begin(ShapeRenderer.ShapeType.Filled);
		stage.act(delta);
		stage.draw();

		steps.setText("number of steps: " + player.getSteps());

		/*
		game.getSpriteBatch().begin();
		game.getFont().draw(game.getSpriteBatch(), "This is the game screen!", 10, 780);
		game.getFont().draw(game.getSpriteBatch(),
				Integer.toString(player.getSteps()), 770, 20);
		game.getSpriteBatch().end();
		*/


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

		game.getShapeRenderer().end();

		if (!won && player.hasWon()) {
			won = true;
			winDialogOptimal.show(stage);
		}

		// Process game logic and input.
		process();
	}

	@Override
	public void show() {
		stage.getViewport().setCamera(camera);
		camera.update();
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
