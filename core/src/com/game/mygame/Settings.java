package com.game.mygame;

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
	private String resolution;
	private int resolutionWidth, resolutionHeight;
	private boolean fullscreen;
	private boolean vSync;

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
			resolution = root.get("resolution");
			String[] resParts = resolution.split("x");
			resolutionWidth = Integer.parseInt(resParts[0]);
			resolutionHeight = Integer.parseInt(resParts[1]);
			fullscreen = root.getBoolean("fullscreen");
			vSync = root.getBoolean("vsync");
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

		xmlWriter.element("resolution", resolution);
		xmlWriter.element("fullscreen", fullscreen);
		xmlWriter.element("vsync", vSync);

		xmlWriter.pop();
		xmlWriter.close();
		printWriter.close();
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

}

