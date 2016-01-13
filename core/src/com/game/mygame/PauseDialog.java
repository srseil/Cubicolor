package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Dialog;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;
import com.badlogic.gdx.utils.Align;

public class PauseDialog extends Dialog {

	MyGame game;
	GameScreen gameScreen;

	public PauseDialog(Skin skin, GameScreen gameScreen, MyGame game) {
		super("Pause", skin);

		this.game = game;
		this.gameScreen = gameScreen;

		this.setMovable(false);
		this.setResizable(false);
		this.setModal(true);

		this.getContentTable().padBottom(10.0f);
		this.getTitleLabel().setAlignment(Align.center);

		TextButton tb = new TextButton("Resume", skin);
		tb.getLabelCell().height(28.0f);
		tb.getLabelCell().padBottom(14.0f);
		this.button(tb, "");
		this.key(Input.Keys.ESCAPE, "");
		this.key(Input.Keys.ENTER, "");
		this.getButtonTable().row();

		tb = new TextButton("Restart", skin);
		tb.getLabelCell().height(28.0f);
		tb.getLabelCell().padBottom(14.0f);
		this.button(tb, "restart");
		this.key(Input.Keys.R, "restart");
		this.getButtonTable().row();

		tb = new TextButton("Main Menu", skin);
		tb.getLabelCell().height(28.0f);
		tb.getLabelCell().padBottom(14.0f);
		this.button(tb, "menu");
	}

	protected void result(Object object) {
		switch ((String) object) {
			case "restart":
				gameScreen.resetLevel();
				break;
			case "menu":
				game.setScreen(game.getMenuScreen());
		}
		gameScreen.setPaused(false);
		gameScreen.setPauseClosed(true);
	}

}

