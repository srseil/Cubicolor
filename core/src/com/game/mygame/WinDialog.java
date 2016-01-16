package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.scenes.scene2d.ui.Dialog;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
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

		Label label = new Label("You completed the level!", skin);
		label.setColor(Color.BLACK);
		this.text(label);
		this.getContentTable().row();
		TextButton tb = new TextButton("Main Menu", skin);
		tb.getLabelCell().height(28.0f);
		tb.getLabelCell().padBottom(14.0f);
		this.button(tb, "menu");
		this.key(Input.Keys.ENTER, "menu");

		if (optimal) {
			label = new Label("You found the optimal solution!", skin);
			label.setColor(Color.BLACK);
			this.text(label);
			this.getContentTable().row();
		} else {
			tb = new TextButton("Retry", skin);
			tb.getLabelCell().height(28.0f);
			tb.getLabelCell().padBottom(14.0f);
			this.button(tb, "retry");
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

