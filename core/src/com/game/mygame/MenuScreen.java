package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.Screen;
import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.OrthographicCamera;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.utils.viewport.ExtendViewport;

public class MenuScreen implements Screen {

	// DISPOSE! -> Documentation

	private MyGame game;
	private Stage stage;
	private OrthographicCamera camera;
	private Skin skin;
	private Table rootTable;
	private Table currentMenu;

	public MenuScreen(MyGame game) {
		this.game = game;
		stage = new Stage(new ExtendViewport(800, 600));
		camera = (OrthographicCamera) stage.getCamera();
		skin = game.getSkin();

		rootTable = new Table();
		rootTable.setFillParent(true);
		rootTable.setDebug(true);
		rootTable.left();
		stage.addActor(rootTable);

		final LevelMenu levelMenu = new LevelMenu(skin, game);
		final SettingsMenu settingsMenu = new SettingsMenu(skin, stage, game);

		// Quit dialog (Exit <-> Quit)
		final Dialog quitDialog = new Dialog("Quit", skin) {
			protected void result(Object object) {
				if ((Boolean) object)
					Gdx.app.exit();
			}
		};
		quitDialog.text("Are you sure you want to quit?");
		quitDialog.button("Quit", true);
		quitDialog.button("Cancel", false);
		quitDialog.key(Input.Keys.ENTER, true);
		quitDialog.key(Input.Keys.ESCAPE, false);

		// Group of menu buttons
		VerticalGroup menuItems = new VerticalGroup();
		menuItems.fill();

		// Play button
		TextButton playButton = new TextButton("Play", skin);
		playButton.setWidth(100.0f);
		playButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				rootTable.removeActor(currentMenu);
				rootTable.add(levelMenu);
				currentMenu = levelMenu;
			}
		});
		menuItems.addActor(playButton);

		// Settings button
		TextButton settingsButton = new TextButton("Settings", skin);
		settingsButton.setWidth(100.0f);
		settingsButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				rootTable.removeActor(currentMenu);
				rootTable.add(settingsMenu);
				currentMenu = settingsMenu;
				System.out.println(rootTable.getRows());
			}
		});
		menuItems.addActor(settingsButton);

		// Quit button
		TextButton quitButton = new TextButton("Quit", skin);
		quitButton.setWidth(100.0f);
		quitButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				quitDialog.show(stage);
			}
		});
		menuItems.addActor(quitButton);

		rootTable.add(menuItems).expandY().padLeft(50.0f);
		rootTable.add(levelMenu).expandY().padLeft(50.0f);
		currentMenu = levelMenu;
		//stage.addActor(currentMenu);
	}


	@Override
	public void show() {
		camera.setToOrtho(false, 800, 600);
		//stage.getViewport().setCamera(camera);
		//camera.update();
		Gdx.input.setInputProcessor(stage);
	}

	@Override
	public void render(float delta) {
		//camera.update();
		Gdx.gl.glClearColor(0.85f, 0.8f, 0.7f, 1);
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT);

		// Update and draw menu content.
		stage.act(delta);
		stage.draw();
	}

	@Override
	public void resize(int width, int height) {
		stage.getViewport().update(width, height, true);
	}

	@Override
	public void pause() {

	}

	@Override
	public void resume() {

	}

	@Override
	public void hide() {

	}

	@Override
	public void dispose() {
		stage.dispose();
	}

}

