package com.game.mygame.client;

import com.badlogic.gdx.ApplicationListener;
import com.badlogic.gdx.backends.gwt.GwtApplication;
import com.badlogic.gdx.backends.gwt.GwtApplicationConfiguration;
import com.game.mygame.MyGame;

public class HtmlLauncher extends GwtApplication {

        @Override
        public GwtApplicationConfiguration getConfig () {
                GwtApplicationConfiguration config = new GwtApplicationConfiguration(800, 600);
                config.alpha = true;
				config.antialiasing = true;
			config.premultipliedAlpha = true;
                return config;
        }

        @Override
        public ApplicationListener getApplicationListener () {
                return new MyGame();
        }
}