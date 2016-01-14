package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.ChangeListener;
import com.badlogic.gdx.scenes.scene2d.utils.TextureRegionDrawable;
import com.badlogic.gdx.utils.Align;

public class SettingsMenu extends Table {

	private final Dialog resetDialog;
	private Stage stage;
	private Skin skin;

	public SettingsMenu(Skin skin, final Stage stage, final MyGame game) {
		super(skin);
		this.stage = stage;
		this.skin = skin;

		// Draw background texture.
		this.setBackground(game.getMenuBackground());

		// Confirmation dialog for resetting progress
		final Dialog confirmResetDialog =
				new Dialog("Reset Progress", skin);
		confirmResetDialog.getTitleLabel().setAlignment(Align.center);
		confirmResetDialog.text("Progress has been reset.");
		TextButton tb = new TextButton("OK", skin);
		tb.getLabelCell().height(28.0f);
		tb.getLabelCell().padBottom(14.0f);
		confirmResetDialog.button(tb, null);
		confirmResetDialog.key(Input.Keys.ENTER, null);
		confirmResetDialog.key(Input.Keys.ESCAPE, null);

		// Dialog for "Reset progress" button
		resetDialog = new Dialog("Reset Progress", skin) {
			@Override
			protected void result(Object object) {
				if ((Boolean) object) {
					game.getSaveState().reset();
					/*
					try {
						game.getSaveState().save();
					} catch (IOException e) {
						e.printStackTrace();
					}
					*/
					confirmResetDialog.show(stage);
				}
			}
		};
		resetDialog.getTitleLabel().setAlignment(Align.center);
		resetDialog.text("This will reset all your level progress and lock\n" +
			"all the levels you have unlocked to far!");
		tb = new TextButton("Reset", skin);
		tb.getLabelCell().height(28.0f);
		tb.getLabelCell().padBottom(14.0f);
		resetDialog.button(tb, true);
		tb = new TextButton("Cancel", skin);
		tb.getLabelCell().height(28.0f);
		tb.getLabelCell().padBottom(14.0f);
		resetDialog.button(tb, false);
		resetDialog.key(Input.Keys.ESCAPE, false);

		createGameSection();
		createAudioSection();
		createVideoSection();
	}

	private void createGameSection() {
		// Section label
		Label label = new Label("Game", skin);
		label.setColor(Color.BLACK);
		add(label).left();
		row();

		// Language drop down menu
		label = new Label("Language", skin);
		label.setColor(Color.BLACK);
		add(label).left();
		row();
		CustomSelectBox<String> languageDropdown = new CustomSelectBox<String>(skin);
		languageDropdown.setItems("English", "Deutsch", "Espanol", "Russian");
		add(languageDropdown).left();

		// Reset progress button
		TextButton resetButton = new TextButton("Reset Progress", skin);
		resetButton.getLabelCell().height(28.0f);
		resetButton.getLabelCell().padBottom(14.0f);
		resetButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				resetDialog.show(stage);
			}
		});
		add(resetButton).left().padLeft(20.0f);
		row();
	}

	private void createAudioSection() {
		// Section label
		Label label = new Label("Audio", skin);
		label.setColor(Color.BLACK);
		add(label).left().padTop(20.0f);
		row();

		// Music and sound volume sliders
		label = new Label("Music Volume", skin);
		label.setColor(Color.BLACK);
		add(label).left();
		label = new Label("Sound Volume", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(20.0f);
		row();
		Slider musicSlider = new Slider(0.0f, 100.0f, 5.0f, false, skin);
		add(musicSlider);
		Slider soundSlider = new Slider(0.0f, 100.0f, 5.0f, false, skin);
		add(soundSlider).padLeft(20.0f);
		row();

		// Music and sound mute buttons and value labels
		Table musicTable = new Table();
		CheckBox mute = new CheckBox("Mute", skin);
		mute.getLabel().setColor(Color.BLACK);
		musicTable.add(mute).expandX().left();
		label = new Label("100", skin);
		label.setColor(Color.BLACK);
		musicTable.add(label).right();
		Table soundTable = new Table();
		mute = new CheckBox("Mute", skin);
		mute.getLabel().setColor(Color.BLACK);
		soundTable.add(mute).expandX().left().padLeft(20.0f);
		label = new Label("100", skin);
		label.setColor(Color.BLACK);
		soundTable.add(label).right();
		add(musicTable).fillX();
		add(soundTable).fillX();
		row();

	}

	private void createVideoSection() {
		// Section label
		Label label = new Label("Video", skin);
		label.setColor(Color.BLACK);
		add(label).left().padTop(20.0f);
		row();

		// Resolution drop down menu and fullscreen checkbox
		final CheckBox fullscreen = new CheckBox("Fullscreen", skin);
		label = new Label("Resolution", skin);
		label.setColor(Color.BLACK);
		add(label).left();
		row();
		final CustomSelectBox<String> resolutionDropdown = new CustomSelectBox<String>(skin);
		resolutionDropdown.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				/*
				String[] res = resolutionDropdown.getSelected().split("x");
				int width = Integer.parseInt(res[0]);
				int height = Integer.parseInt(res[1]);
				Gdx.graphics.setDisplayMode(
						width, height, fullscreen.isChecked());
						*/
			}
		});
		resolutionDropdown.setItems("800x600", "1440x1080", "1280x720", "800x600");
		resolutionDropdown.getScrollPane().setColor(Color.BLACK);
		resolutionDropdown.getScrollPane().setHeight(30.0f);
		resolutionDropdown.getStyle().fontColor = Color.BLACK;
		resolutionDropdown.setHeight(30.0f);
		resolutionDropdown.setSize(10.0f, 10.0f);
		resolutionDropdown.sizeBy(10.0f, 10.0f);
		add(resolutionDropdown).left();
		fullscreen.getLabel().setColor(Color.BLACK);
		add(fullscreen).left().padLeft(20.0f);
		row();

		// Brightness and contrast sliders
		label = new Label("Brightness", skin);
		label.setColor(Color.BLACK);
		add(label).left();
		label = new Label("Contrast", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(20.0f);
		row();
		add(new Slider(0.0f, 100.0f, 5.0f, false, skin));
		add(new Slider(0.0f, 100.0f, 5.0f, false, skin)).padLeft(20.0f);
		row();

		// VSync and colorblind mode checkboxes
		CheckBox box = new CheckBox("VSync", skin);
		box.getLabel().setColor(Color.BLACK);
		add(box).left();
		box = new CheckBox("Colorblind Mode", skin);
		box.getLabel().setColor(Color.BLACK);
		add(box).left().padLeft(20.0f);
	}

}

