package com.game.mygame;

import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;
import com.badlogic.gdx.scenes.scene2d.ui.TextButton;
import com.badlogic.gdx.utils.Align;

public class CreditsMenu extends Table {

	public CreditsMenu(Skin skin, MyGame game) {
		super(skin);
		setBackground("menu-background");

		Label l = new Label("[game logo goes here]", skin);
		l.setColor(Color.BLACK);
		add(l).colspan(2);
		row();
		l = new Label("A game by Stefan Seil.", skin);
		l.setColor(Color.BLACK);
		add(l).colspan(2).expandX().align(Align.center).padTop(10f).padBottom(10f);
		row();
		l = new Label("@Moltenplay", skin);
		l.setColor(Color.BLACK);
		add(l).left();
		l = new Label("Moltenplay.com", skin);
		l.setColor(Color.BLACK);
		add(l).right().padLeft(30.0f);

		//debug();
	}

}

