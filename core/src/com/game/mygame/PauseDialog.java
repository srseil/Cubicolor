package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.scenes.scene2d.Stage;
import com.badlogic.gdx.scenes.scene2d.ui.Dialog;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;

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

		this.button(new TextButton("Resume", skin), "");
		this.key(Input.Keys.ESCAPE, "");
		this.key(Input.Keys.ENTER, "");
		this.getButtonTable().row();
		this.button(new TextButton("Restart", skin), "restart");
		this.getButtonTable().row();
		this.button(new TextButton("Main Menu", skin), "menu");
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
