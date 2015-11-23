package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.Texture;
import com.badlogic.gdx.graphics.g2d.Batch;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;
import com.badlogic.gdx.scenes.scene2d.Actor;
import com.badlogic.gdx.scenes.scene2d.utils.Drawable;

public class GameBoard extends Actor implements Drawable {

	private Level level;
	private Player player;
	private ShapeRenderer shapeRenderer;
	private float width, height;

	private Texture texure;

	public GameBoard(Level level, Player player, ShapeRenderer shapeRenderer) {
		this.level = level;
		this.player = player;
		this.shapeRenderer = shapeRenderer;
		width = (float) level.getColumns() * 50;
		height = (float) level.getRows() * 50;

		texure = new Texture(Gdx.files.internal("avatar.png"));
	}

	@Override
	public void draw(Batch batch, float parentAlpha) {
		batch.end();
		shapeRenderer.end();

		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		shapeRenderer.setProjectionMatrix(batch.getProjectionMatrix());
		shapeRenderer.setTransformMatrix(batch.getTransformMatrix());
		//shapeRenderer.rect(getX(), getY(), 50, 50);

		for (int i = 0; i < level.getRows(); i++) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!level.getMatrix()[i][j].isDead()) {
					level.getMatrix()[i][j].draw(getX() - width/2 + j*50.0f,
							getY() - height/2 + i*50.0f, shapeRenderer);
				}
			}
		}

		player.draw(getX() - width/2, getY() - height/2, shapeRenderer);
		shapeRenderer.end();
		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		batch.begin();
	}

	@Override
	public void draw(Batch batch, float x, float y, float width, float height) {
		shapeRenderer.end();
		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		shapeRenderer.setProjectionMatrix(batch.getProjectionMatrix());
		shapeRenderer.setTransformMatrix(batch.getTransformMatrix());
		shapeRenderer.setColor(0.2f, 0.4f, 0.1f, 1.0f);
		shapeRenderer.rect(getX(), getY(), 50, 50);
/*
		for (int i = 0; i < rows; i++) {
			for (int j = 0; j < columns; j++) {
				if (!matrix[i][j].isDead()) {
					//matrix[i][j].draw(200f + j*50f, 100f + i*50f, shapeRenderer);
					matrix[i][j].draw(j*50f, i*50f, shapeRenderer);
				}
			}
		}
*/
		shapeRenderer.end();
		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
	}

	@Override
	public float getLeftWidth() {
		return 0;
	}

	@Override
	public void setLeftWidth(float leftWidth) {

	}

	@Override
	public float getRightWidth() {
		return 0;
	}

	@Override
	public void setRightWidth(float rightWidth) {

	}

	@Override
	public float getTopHeight() {
		return 0;
	}

	@Override
	public void setTopHeight(float topHeight) {

	}

	@Override
	public float getBottomHeight() {
		return 0;
	}

	@Override
	public void setBottomHeight(float bottomHeight) {

	}

	@Override
	public float getMinWidth() {
		return 0;
	}

	@Override
	public void setMinWidth(float minWidth) {

	}

	@Override
	public float getMinHeight() {
		return 0;
	}

	@Override
	public void setMinHeight(float minHeight) {

	}
}
