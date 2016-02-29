package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.scenes.scene2d.ui.Dialog;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;
import com.badlogic.gdx.utils.Align;

public class WinDialog extends Dialog {

	MyGame game;
	GameScreen gameScreen;
	Level level;

	public WinDialog(boolean optimal, Skin skin, Level level, GameScreen gameScreen, MyGame game) {
		super("Completed", skin);

		this.game = game;
		this.gameScreen = gameScreen;
		this.level = level;

		this.setMovable(false);
		this.setResizable(false);
		this.setModal(true);

		getTitleLabel().setAlignment(Align.center);

		Label label = new Label("You completed the level!", skin);
		label.setColor(Color.BLACK);
		text(label);
		//getContentTable().row();

		/*
		if (optimal) {
			label = new Label("You found the optimal solution!", skin);
			label.setColor(Color.BLACK);
			this.text(label);
			this.getContentTable().row();
		} else {
		}
		*/

		TextButton tb = new TextButton("Main Menu", skin);
		tb.addListener(game.createClickListener());
		key(Input.Keys.ESCAPE, "menu");
		button(tb, "menu").setWidth(120.0f);

		TextButton retry = new TextButton("Retry", skin);
		retry.addListener(game.createClickListener());
		key(Input.Keys.R, "retry");
		button(retry, "retry");

		if (level.getNumber() != 16) {
			TextButton next = new TextButton("Next Level", skin);
			next.addListener(game.createClickListener());
			key(Input.Keys.ENTER, "next");
			button(next, "next");
		}
	}

	protected void result(Object object) {
		switch ((String) object) {
			case "menu":
				//game.setScreen(game.getMenuScreen());
				game.toMenuScreen();
				//gameScreen.resetLevel();
				break;
			case "retry":
				gameScreen.resetLevel();
				break;
			case "next":
				if (level.getNumber() == 16) {
					if (level.getDifficulty() == Difficulty.NORMAL)
						game.openLevel(Difficulty.SMART, 1);
					else
						game.openLevel(Difficulty.GENIUS, 1);
				} else {
					game.openLevel(level.getDifficulty(), level.getNumber()+1);
				}
		}
	}
}

