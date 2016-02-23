package com.game.mygame;

import com.badlogic.gdx.Input;
import com.badlogic.gdx.files.FileHandle;
import com.badlogic.gdx.utils.GdxRuntimeException;
import com.badlogic.gdx.utils.XmlReader;
import com.badlogic.gdx.utils.XmlWriter;

import java.io.File;
import java.io.IOException;
import java.io.PrintWriter;

public class Settings {

	private XmlReader reader;
	private String path;
	// Settings values
	private boolean musicMuted, soundMuted;
	private int musicVolume, soundVolume;
	private String resolution;
	private int resolutionWidth, resolutionHeight;
	private boolean fullscreen;
	private boolean vSync;
	// Control values
	private int forwards, backwards, left, right, restart, pause;

	public Settings() {
		reader = new XmlReader();
		path = System.getProperty("user.dir") + "/config/config.xml";
	}

	/*
	 * Load settings from file.
	 */
	public void load() throws IOException {
		XmlReader.Element root = reader.parse(new FileHandle(path));

		try {
			// Game settings
			root = root.getChildByName("settings");
			musicMuted = root.getBoolean("music-muted");
			musicVolume = Integer.parseInt(root.get("music-volume"));
			soundMuted = root.getBoolean("sound-muted");
			soundVolume = Integer.parseInt(root.get("sound-volume"));
			resolution = root.get("resolution");
			String[] resParts = resolution.split("x");
			resolutionWidth = Integer.parseInt(resParts[0]);
			resolutionHeight = Integer.parseInt(resParts[1]);
			fullscreen = root.getBoolean("fullscreen");
			vSync = root.getBoolean("vsync");
			// Controls settings
			root = root.getParent().getChildByName("controls");
			forwards = root.getInt("forwards");
			backwards = root.getInt("backwards");
			left = root.getInt("left");
			right = root.getInt("right");
			restart = root.getInt("restart");
			pause = root.getInt("pause");
		} catch (GdxRuntimeException exception) {
			exception.printStackTrace();
			throw new IOException();
		}
	}

	/*
	 * Save settings to file.
	 */
	public void save() throws IOException {
		PrintWriter printWriter = new PrintWriter(new File(path));
		printWriter.write("<?xml version=\"1.1\" encoding=\"UTF-8\"?>\n");

		XmlWriter xmlWriter = new XmlWriter(printWriter);
		xmlWriter.element("config");

		// Game settings
		xmlWriter.element("settings");
		xmlWriter.element("music-muted", musicMuted);
		xmlWriter.element("music-volume", musicVolume);
		xmlWriter.element("sound-muted", soundMuted);
		xmlWriter.element("sound-volume", soundVolume);
		xmlWriter.element("resolution", resolution);
		xmlWriter.element("fullscreen", fullscreen);
		xmlWriter.element("vsync", vSync);
		xmlWriter.pop();
		// Controls settings
		xmlWriter.element("controls");
		xmlWriter.element("forwards", forwards);
		xmlWriter.element("backwards", backwards);
		xmlWriter.element("left", left);
		xmlWriter.element("right", right);
		xmlWriter.element("restart", restart);
		xmlWriter.element("pause", pause);
		xmlWriter.pop();

		xmlWriter.pop();
		xmlWriter.close();
		printWriter.close();
	}

	public void setMusicMuted(boolean muted) {
		musicMuted = muted;
	}

	public void setMusicVolume(int volume) {
		musicVolume = volume;
	}

	public void setSoundMuted(boolean muted) {
		soundMuted = muted;
	}

	public void setSoundVolume(int volume) {
		soundVolume = volume;
	}

	public void setResolution(String resolution) {
		this.resolution = resolution;
	}

	public void setFullscreenEnabled(boolean fullscreen) {
		this.fullscreen = fullscreen;
	}

	public void setvSyncEnabled(boolean vSync) {
		this.vSync = vSync;
	}

	public void setForwardsButtons(int forwards) {
		this.forwards = forwards;
	}

	public void setBackwardsButton(int backwards) {
		this.backwards = backwards;
	}

	public void setLeftButton(int left) {
		this.left = left;
	}

	public void setRightButton(int right) {
		this.right = right;
	}

	public void setRestartButton(int restart) {
		this.restart = restart;
	}

	public void setPauseButton(int pause) {
		this.pause = pause;
	}

	// Getters:

	public boolean getMusicMuted() {
		return musicMuted;
	}

	public int getMusicVolume() {
		return musicVolume;
	}

	public boolean getSoundMuted() {
		return soundMuted;
	}

	public int getSoundVolume() {
		return soundVolume;
	}

	public String getResolution() {
		return resolution;
	}

	public int getResolutionWidth() {
		return resolutionWidth;
	}

	public int getResolutionHeight() {
		return resolutionHeight;
	}

	public boolean getFullscreenEnabled() {
		return fullscreen;
	}

	public boolean getVSyncEnabled() {
		return vSync;
	}

	public int getForwardsButton() {
		return forwards;
	}

	public int getBackwardsButton() {
		return backwards;
	}

	public int getLeftButton() {
		return left;
	}

	public int getRightButton() {
		return right;
	}

	public int getRestartButton() {
		return restart;
	}

	public int getPauseButton() {
		return pause;
	}

}

