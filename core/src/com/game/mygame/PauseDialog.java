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

		TextButton menu = new TextButton("Main Menu", skin);
		menu.getLabelCell().height(28.0f);
		menu.getLabelCell().padBottom(14.0f);

		TextButton resume = new TextButton("Resume", skin);
		resume.getLabelCell().width(menu.getLabelCell().getPrefWidth());
		resume.getLabelCell().height(28.0f);
		resume.getLabelCell().padBottom(14.0f);

		TextButton restart = new TextButton("Restart", skin);
		restart.getLabelCell().width(menu.getLabelCell().getPrefWidth());
		restart.getLabelCell().height(28.0f);
		restart.getLabelCell().padBottom(14.0f);

		this.button(resume, "");
		this.key(Input.Keys.ESCAPE, "");
		this.key(Input.Keys.ENTER, "");
		this.getButtonTable().row();

		this.button(restart, "restart");
		this.key(Input.Keys.R, "restart");
		this.getButtonTable().row();

		this.button(menu, "menu");
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

