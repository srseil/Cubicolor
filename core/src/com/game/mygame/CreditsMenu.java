package com.game.mygame;

import com.badlogic.gdx.graphics.Color;
import com.badlogic.gdx.scenes.scene2d.ui.Label;
import com.badlogic.gdx.scenes.scene2d.ui.Skin;
import com.badlogic.gdx.scenes.scene2d.ui.Table;

public class CreditsMenu extends Table {

	public CreditsMenu(Skin skin, MyGame game) {
		super(skin);
		setBackground("menu-background");

		Label l = new Label("Game logo goes here.", skin);
		l.setColor(Color.BLACK);
		add(l);
		row();
		l = new Label("A game by Stefan Seil", skin);
		l.setColor(Color.BLACK);
		add(l);
		row();
		l = new Label("And the company logo\nas well as website and twitter.", skin);
		l.setColor(Color.BLACK);
		add(l);
	}

}

