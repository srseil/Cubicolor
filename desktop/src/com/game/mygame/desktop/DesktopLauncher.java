package com.game.mygame.desktop;

import com.badlogic.gdx.backends.lwjgl.LwjglApplication;
import com.badlogic.gdx.backends.lwjgl.LwjglApplicationConfiguration;
import com.badlogic.gdx.tools.texturepacker.TexturePacker;
import com.game.mygame.MyGame;
import com.game.mygame.Settings;

public class DesktopLauncher {

	public static void main (String[] arg) {
		// Create game instance and load settings along with it
		MyGame myGame = new MyGame();
		Settings settings = myGame.getSettings();

		LwjglApplicationConfiguration cfg = new LwjglApplicationConfiguration();
		cfg.title = "MyGame";
		cfg.samples = 2;

		cfg.width = settings.getResolutionWidth();
		cfg.height = settings.getResolutionHeight();
		cfg.fullscreen = settings.getFullscreenEnabled();
		cfg.vSyncEnabled = settings.getVSyncEnabled();

		//cfg.foregroundFPS = 0;
		//cfg.backgroundFPS = 0;

		TexturePacker.Settings tsettings = new TexturePacker.Settings();
		tsettings.paddingX = 0;
		tsettings.paddingY = 0;
		tsettings.maxHeight = 512;
		tsettings.maxWidth = 512;

		TexturePacker.processIfModified(tsettings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_red",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_red", "player_red");
		TexturePacker.processIfModified(tsettings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_green",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_green", "player_green");
		TexturePacker.processIfModified(tsettings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_blue",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_blue", "player_blue");
		TexturePacker.processIfModified(tsettings, "/home/stefan/libGDX/MyGame/android/assets/animations/player_yellow",
				"/home/stefan/libGDX/MyGame/android/assets/animations/player_yellow", "player_yellow");

		TexturePacker.processIfModified(tsettings, "/home/stefan/libGDX/MyGame/android/assets/textures/keytile",
				"/home/stefan/libGDX/MyGame/android/assets/textures/keytile", "keytile");
		TexturePacker.processIfModified(tsettings, "/home/stefan/libGDX/MyGame/android/assets/textures/e/",
				"/home/stefan/libGDX/MyGame/android/assets/textures/exittile/", "exittile");

		TexturePacker.processIfModified(tsettings, "/home/stefan/Bilder/MyGame/skin/ninepatches/",
				"/home/stefan/libGDX/MyGame/android/assets/skins/", "uiskin");

		new LwjglApplication(myGame, cfg);
	}

}
