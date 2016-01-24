package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.Input;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.graphics.g2d.NinePatch;
import com.badlogic.gdx.graphics.g2d.TextureRegion;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.scenes.scene2d.*;
import com.badlogic.gdx.scenes.scene2d.ui.*;
import com.badlogic.gdx.scenes.scene2d.utils.*;
import com.badlogic.gdx.utils.Align;

import java.io.IOException;
import java.util.ArrayList;

public class SettingsMenu extends Table {

	private final MyGame game;
	private final Dialog resetDialog, resolutionDialog;
	private Stage stage;
	private Skin skin;
	private Label.LabelStyle headerStyle, contentStyle;
	// Settings content
	private String[] resolutions;

	public SettingsMenu(Skin skin, final Stage stage, final MyGame game) {
		super(skin);
		this.game = game;
		this.stage = stage;
		this.skin = skin;

		headerStyle = new Label.LabelStyle(
				game.getBitmapFont("Vollkorn-Italic-32"), Color.BLACK);
		contentStyle = new Label.LabelStyle(
				game.getBitmapFont("Vollkorn-Regular-32"), Color.BLACK);

		String[] res = {"1920x1080", "1440x1080", "1280x720", "800x600"};
		resolutions = res;

		// Set background texture.
		NinePatch backgroundPatch =
				game.getSkin().get("menu-background", NinePatch.class);
		NinePatchDrawable background = new NinePatchDrawable(backgroundPatch);
		this.setBackground(background);

		resolutionDialog = new Dialog("Resolution Changed", skin);
		resolutionDialog.getTitleLabel().setAlignment(Align.center);
		Label l = new Label("You need to restart the game\nto apply the resolution.", skin);
		l.setColor(Color.BLACK);
		resolutionDialog.text(l);
		/*
		resolutionDialog.text(
				"You need to restart the game\nto apply the resolution.",
				contentStyle);
				*/
		resolutionDialog.button(new TextButton("OK", skin), null);
		resolutionDialog.key(Input.Keys.ENTER, null);
		resolutionDialog.key(Input.Keys.ESCAPE, null);

		// Confirmation dialog for resetting progress
		final Dialog confirmResetDialog =
				new Dialog("Reset Progress", skin);
		confirmResetDialog.getTitleLabel().setAlignment(Align.center);
		Label confirmResetLabel = new Label("Progress has been reset.", skin);
		confirmResetLabel.setColor(Color.BLACK);
		confirmResetDialog.text(confirmResetLabel);
		TextButton tb = new TextButton("OK", skin);
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
		Label resetLabel =
				new Label("This will reset all your level progress and lock\n" +
			"all the levels you have unlocked to far!", skin);
		resetLabel.setColor(Color.BLACK);
		resetDialog.text(resetLabel);
		tb = new TextButton("Reset", skin);
		resetDialog.button(tb, true);
		tb = new TextButton("Cancel", skin);
		resetDialog.button(tb, false);
		resetDialog.key(Input.Keys.ESCAPE, false);

		createGameSection();
		createAudioSection();
		createVideoSection();
	}

	private void createGameSection() {
		// Section label
		Label label = new Label("Game", headerStyle);
		//label.setStyle(italicStyle);
		//label.setColor(Color.BLACK);
		add(label).left();
		row();

		// Language drop down menu
		label = new Label("Language", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(15.0f);
		row();
		CustomSelectBox<String> languageDropdown = new CustomSelectBox<String>(skin);
		languageDropdown.setItems("English", "Deutsch", "Espanol", "Russian");
		add(languageDropdown).left().padLeft(15.0f);

		// Reset progress button
		TextButton resetButton = new TextButton("Reset Progress", skin);
		resetButton.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				resetDialog.show(stage);
			}
		});
		add(resetButton).left().padLeft(20.0f).padTop(10.0f);
		row();
	}

	private void createAudioSection() {
		// Section label
		Label label = new Label("Audio", headerStyle);
		add(label).left().padTop(20.0f);
		row();

		// Music and sound volume sliders
		label = new Label("Music Volume", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(15.0f);
		label = new Label("Sound Volume", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(20.0f);
		row();


		final Label musicVolume = new Label(String.valueOf(game.getSettings().getMusicVolume()), skin);
		musicVolume.setColor(Color.BLACK);
		final Label soundVolume = new Label(String.valueOf(game.getSettings().getSoundVolume()), skin);
		soundVolume.setColor(Color.BLACK);
		final Slider musicSlider = new Slider(0.0f, 100.0f, 1.0f, false, skin);
		musicSlider.setValue(game.getSettings().getMusicVolume());
		musicSlider.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				game.getMusic().setVolume(musicSlider.getValue() / 100.0f);
				musicVolume.setText(
						String.valueOf((int) musicSlider.getValue()));
			}
		});
		musicSlider.addListener(new ClickListener() {
			@Override
			public void touchUp(InputEvent event, float x, float y,
								int pointer, int button) {
				super.touchUp(event, x, y, pointer, button);
				game.getSettings().setMusicVolume((int) musicSlider.getValue());
				try {
					game.getSettings().save();
					System.out.println("Music Volume changed.");
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		});
		add(musicSlider).padLeft(15.0f);
		final Slider soundSlider = new Slider(0.0f, 100.0f, 1.0f, false, skin);
		soundSlider.setValue(game.getSettings().getSoundVolume());
		soundSlider.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				game.getSound("Player-Step").play(
						soundSlider.getValue() / 100.0f);
				//game.setSoundVolume(soundSlider.getValue() / 100.0f);
				soundVolume.setText(
						String.valueOf((int) soundSlider.getValue()));
			}
		});
		soundSlider.addListener(new ClickListener() {
			@Override
			public void touchUp(InputEvent event, float x, float y,
								int pointer, int button) {
				super.touchUp(event, x, y, pointer, button);
				game.getSettings().setSoundVolume((int) soundSlider.getValue());
				try {
					game.getSettings().save();
					System.out.println("Sound Volume changed.");
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		});
		add(soundSlider).padLeft(20.0f);
		row();

		// Music and sound mute buttons and value labels
		Table musicTable = new Table();
		final CheckBox musicMute = new CheckBox("Mute", skin);
		musicMute.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				boolean muted = musicMute.isChecked();
				if (muted)
					game.getMusic().pause();
				else
					game.getMusic().play();
				game.getSettings().setMusicMuted(muted);
				try {
					game.getSettings().save();
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		});
		musicMute.setChecked(game.getSettings().getMusicMuted());
		musicMute.getLabel().setColor(Color.BLACK);
		musicTable.add(musicMute).expandX().left().padLeft(15.0f);
		musicTable.add(musicVolume).right();
		Table soundTable = new Table();
		final CheckBox soundMute = new CheckBox("Mute", skin);
		soundMute.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				boolean muted = soundMute.isChecked();
				//game.setSoundMuted(muted);
				game.getSettings().setSoundMuted(muted);
				try {
					game.getSettings().save();
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		});
		soundMute.setChecked(game.getSettings().getSoundMuted());
		soundMute.getLabel().setColor(Color.BLACK);
		soundTable.add(soundMute).expandX().left().padLeft(20.0f);
		soundTable.add(soundVolume).right();
		add(musicTable).fillX();
		add(soundTable).fillX();
		row();

	}

	private void createVideoSection() {
		// Section label
		Label label = new Label("Video", headerStyle);
		add(label).left().padTop(20.0f);
		row();

		// Resolution drop down menu
		final CheckBox fullscreen = new CheckBox("Fullscreen", skin);
		label = new Label("Resolution", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(15.0f);
		row();
		final CustomSelectBox<String> resolutionDropdown
				= new CustomSelectBox<>(skin);
		resolutionDropdown.setItems(resolutions);
		resolutionDropdown.setSelected(game.getSettings().getResolution());
		resolutionDropdown.getScrollPane().setColor(Color.BLACK);
		resolutionDropdown.getStyle().fontColor = Color.BLACK;
		resolutionDropdown.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				try {
					System.out.println("Resolution changed.");
					String resolution = resolutionDropdown.getSelected();
					game.getSettings().setResolution(resolution);
					game.getSettings().save();
					resolutionDialog.show(stage);
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		});
		add(resolutionDropdown).left().padLeft(15.0f);
		// Fullscreen checkbox
		fullscreen.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				CheckBox checkBox = (CheckBox) actor;
				if (checkBox.isChecked()) {
					Gdx.graphics.setDisplayMode(Gdx.graphics.getWidth(),
							Gdx.graphics.getHeight(), true);
					game.getSettings().setFullscreenEnabled(true);
				} else {
					Gdx.graphics.setDisplayMode(Gdx.graphics.getWidth(),
							Gdx.graphics.getHeight(), false);
					game.getSettings().setFullscreenEnabled(false);
				}
				try {
					game.getSettings().save();
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		});
		fullscreen.setChecked(game.getSettings().getFullscreenEnabled());
		fullscreen.getLabel().setColor(Color.BLACK);
		add(fullscreen).left().padLeft(20.0f);
		row();

		/*
		// Brightness and contrast sliders
		label = new Label("Brightness", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(15.0f);
		label = new Label("Contrast", skin);
		label.setColor(Color.BLACK);
		add(label).left().padLeft(20.0f);
		row();
		add(new Slider(0.0f, 100.0f, 5.0f, false, skin));
		add(new Slider(0.0f, 100.0f, 5.0f, false, skin)).padLeft(20.0f);
		row();
		*/

		// VSync and colorblind mode checkboxes
		CheckBox box = new CheckBox("VSync", skin);
		box.addListener(new ChangeListener() {
			@Override
			public void changed(ChangeEvent event, Actor actor) {
				CheckBox checkBox = (CheckBox) actor;
				if (checkBox.isChecked()) {
					Gdx.graphics.setVSync(true);
					game.getSettings().setvSyncEnabled(true);
				} else {
					Gdx.graphics.setVSync(false);
					game.getSettings().setvSyncEnabled(false);
				}
				try {
					game.getSettings().save();
				} catch (IOException e) {
					e.printStackTrace();
				}
			}
		});
		box.setChecked(game.getSettings().getVSyncEnabled());
		box.getLabel().setColor(Color.BLACK);
		add(box).left().padLeft(15.0f);
		box = new CheckBox("Colorblind Mode", skin);
		box.getLabel().setColor(Color.BLACK);
		add(box).left().padLeft(20.0f);
	}

}

