package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.InputProcessor;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.g2d.BitmapFont;
import com.badlogic.gdx.scenes.scene2d.InputEvent;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;
import com.badlogic.gdx.scenes.scene2d.utils.ClickListener;

import java.io.IOException;

public class ControlsMenu extends Table implements InputProcessor {

	private MyGame game;
	private Stage stage;
	private InputProcessor inputProcessor;
	private Settings settings;
	private Label.LabelStyle headerStyle, contentStyle;
	private TextButton.TextButtonStyle buttonStyle;
	private TextButton forwardsButton, backwardsButton,
			leftButton, rightButton, restartButton, pauseButton;

	public ControlsMenu(Skin skin, Stage stage, MyGame game) {
		super(skin);
		this.game = game;
		this.stage = stage;
		inputProcessor = this;
		settings = game.getSettings();
		setBackground("menu-background");

		headerStyle = new Label.LabelStyle(
				skin.get("default", Label.LabelStyle.class));
		headerStyle.font = game.getBitmapFont("Vollkorn-Italic-32");
		headerStyle.fontColor = Color.BLACK;
		contentStyle = new Label.LabelStyle(
				skin.get("default", Label.LabelStyle.class));
		contentStyle.font = skin.get("default-font", BitmapFont.class);
		contentStyle.fontColor = Color.BLACK;
		buttonStyle = skin.get("select", TextButton.TextButtonStyle.class);

		addMovementSection();
		row();
		addLevelSection();
	}

	private void addMovementSection() {
		Label playerLabel = new Label("Player Movement", headerStyle);
		add(playerLabel).left().padTop(-18.0f);
		row();

		// Forwards
		Label forwards = new Label("Forwards:", contentStyle);
		add(forwards).left().padLeft(15.0f);
		String forwardsString = Input.Keys.toString(
				settings.getForwardsButton());
		forwardsButton = new TextButton(forwardsString, buttonStyle);
		forwardsButton.addListener(game.createClickListener());
		forwardsButton.addListener(new ClickListener() {
			@Override
			public void clicked(InputEvent event, float x, float y) {
				forwardsButton.setChecked(true);
				forwardsButton.setText("?");
				Gdx.input.setInputProcessor(inputProcessor);
			}
		});
		add(forwardsButton).width(120.0f).left().padLeft(20.0f);
		row();

		// Backwards
		Label backwards = new Label("Backwards:", contentStyle);
		add(backwards).left().padLeft(15.0f);
		String backwardsString = Input.Keys.toString(
				settings.getBackwardsButton());
		backwardsButton = new TextButton(backwardsString, buttonStyle);
		backwardsButton.addListener(game.createClickListener());
		backwardsButton.addListener(new ClickListener() {
			@Override
			public void clicked(InputEvent event, float x, float y) {
				backwardsButton.setChecked(true);
				backwardsButton.setText("?");
				Gdx.input.setInputProcessor(inputProcessor);
			}
		});
		add(backwardsButton).width(120.0f).left().padLeft(20.0f).padTop(5.0f);
		row();

		// Left
		Label left = new Label("Left:", contentStyle);
		add(left).left().padLeft(15.0f);
		String leftString = Input.Keys.toString(
				settings.getLeftButton());
		leftButton = new TextButton(leftString, buttonStyle);
		leftButton.addListener(game.createClickListener());
		leftButton.addListener(new ClickListener() {
			@Override
			public void clicked(InputEvent event, float x, float y) {
				leftButton.setChecked(true);
				leftButton.setText("?");
				Gdx.input.setInputProcessor(inputProcessor);
			}
		});
		add(leftButton).width(120.0f).left().padLeft(20.0f).padTop(5.0f);
		row();

		// Right
		Label right = new Label("Right:", contentStyle);
		add(right).left().padLeft(15.0f);
		String rightString =
				Input.Keys.toString(settings.getRightButton());
		rightButton = new TextButton(rightString, buttonStyle);
		rightButton.addListener(game.createClickListener());
		rightButton.addListener(new ClickListener() {
			@Override
			public void clicked(InputEvent event, float x, float y) {
				rightButton.setChecked(true);
				rightButton.setText("?");
				Gdx.input.setInputProcessor(inputProcessor);
			}
		});
		add(rightButton).width(120.0f).left().padLeft(20.0f).padTop(5.0f);
	}

	private void addLevelSection() {
		Label levelLabel = new Label("Level Control", headerStyle);
		add(levelLabel).left();
		row();

		// Restart
		Label restart = new Label("Restart:", contentStyle);
		add(restart).left().padLeft(15.0f);
		String restartString =
				Input.Keys.toString(settings.getRestartButton());
		restartButton = new TextButton(restartString, buttonStyle);
		restartButton.addListener(game.createClickListener());
		restartButton.addListener(new ClickListener() {
			@Override
			public void clicked(InputEvent event, float x, float y) {
				restartButton.setChecked(true);
				restartButton.setText("?");
				Gdx.input.setInputProcessor(inputProcessor);
			}
		});
		add(restartButton).width(120.0f).left().padLeft(20.0f).padTop(5.0f);
		row();

		// Pause Menu
		Label pause = new Label("Pause Menu:", contentStyle);
		add(pause).left().padLeft(15.0f);
		String pauseString =
				Input.Keys.toString(settings.getPauseButton());
		pauseButton = new TextButton(pauseString, buttonStyle);
		pauseButton.addListener(game.createClickListener());
		pauseButton.addListener(new ClickListener() {
			@Override
			public void clicked(InputEvent event, float x, float y) {
				pauseButton.setChecked(true);
				pauseButton.setText("?");
				Gdx.input.setInputProcessor(inputProcessor);
			}
		});
		add(pauseButton).width(120.0f).left().padLeft(20.0f).padTop(5.0f);
	}

	@Override
	public boolean keyDown(int keycode) {
		if (forwardsButton.isChecked()) {
			settings.setForwardsButtons(keycode);
			forwardsButton.setChecked(false);
			forwardsButton.setText(Input.Keys.toString(keycode));
		} else if (backwardsButton.isChecked()) {
			settings.setBackwardsButton(keycode);
			backwardsButton.setChecked(false);
			backwardsButton.setText(Input.Keys.toString(keycode));
		} else if (leftButton.isChecked()) {
			settings.setLeftButton(keycode);
			leftButton.setChecked(false);
			leftButton.setText(Input.Keys.toString(keycode));
		} else if (rightButton.isChecked()) {
			settings.setRightButton(keycode);
			rightButton.setChecked(false);
			rightButton.setText(Input.Keys.toString(keycode));
		} else if (restartButton.isChecked()) {
			settings.setRestartButton(keycode);
			restartButton.setChecked(false);
			restartButton.setText(Input.Keys.toString(keycode));
		} else if (pauseButton.isChecked()) {
			settings.setPauseButton(keycode);
			pauseButton.setChecked(false);
			pauseButton.setText(Input.Keys.toString(keycode));
		}

		try {
			settings.save();
		} catch (IOException e) {
			e.printStackTrace();
		}

		Gdx.input.setInputProcessor(stage);
		return true;
	}

	@Override
	public boolean keyUp(int keycode) {
		return false;
	}

	@Override
	public boolean keyTyped(char character) {
		return false;
	}

	@Override
	public boolean touchDown(int screenX, int screenY, int pointer, int button) {
		return false;
	}

	@Override
	public boolean touchUp(int screenX, int screenY, int pointer, int button) {
		return false;
	}

	@Override
	public boolean touchDragged(int screenX, int screenY, int pointer) {
		return false;
	}

	@Override
	public boolean mouseMoved(int screenX, int screenY) {
		return false;
	}

	@Override
	public boolean scrolled(int amount) {
		return false;
	}
}

