package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.scenes.scene2d.ui.Dialog;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;

public class WinDialog extends Dialog {

	MyGame game;
	GameScreen gameScreen;

	public WinDialog(boolean optimal, Skin skin, GameScreen gameScreen, MyGame game) {
		super("Completed", skin);

		this.game = game;
		this.gameScreen = gameScreen;

		this.setMovable(false);
		this.setResizable(false);
		this.setModal(true);

		this.text("You completed the level!");
		this.getContentTable().row();
		this.button(new TextButton("Main Menu", skin), "menu");
		this.key(Input.Keys.ENTER, "menu");

		if (optimal) {
			this.text("You found the optimal solution!");
			this.getContentTable().row();
		} else {
			this.button(new TextButton("Retry", skin), "retry");
			this.key(Input.Keys.ESCAPE, "retry");
			this.key(Input.Keys.R, "retry");
		}
	}

	protected void result(Object object) {
		switch ((String) object) {
			case "menu":
				game.setScreen(game.getMenuScreen());
				gameScreen.resetLevel();
				break;
			case "retry":
				gameScreen.resetLevel();
		}
	}
}
