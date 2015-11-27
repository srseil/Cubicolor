package com.game.mygame;

import com.badlogic.gdx.Gdx;
import com.badlogic.gdx.graphics.*;
import com.badlogic.gdx.graphics.g2d.Batch;
import com.badlogic.gdx.graphics.g3d.Environment;
import com.badlogic.gdx.graphics.g3d.Material;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.ColorAttribute;
import com.badlogic.gdx.graphics.g3d.environment.DirectionalLight;
import com.badlogic.gdx.graphics.glutils.ShapeRenderer;
import com.badlogic.gdx.scenes.scene2d.Actor;

public class GameBoard extends Actor {

	private MyGame game;
	private Level level;
	private Player player;
	private Environment environment;
	private Model model;
	private ModelInstance modelInstance;
	private ModelInstance[][] matrix;
	private float width, height;

	//private PerspectiveCamera camera;
	private OrthographicCamera camera;
	private ShapeRenderer shapeRenderer;

	public GameBoard(Level level, Player player, OrthographicCamera camera, MyGame game, ShapeRenderer shapeRenderer) {
		this.level = level;
		this.player = player;
		this.game = game;
		this.camera = camera;

		this.shapeRenderer = shapeRenderer;

		width = (float) level.getColumns() * 10;
		height = (float) level.getRows() * 10;

		matrix = parseMatrix(level.getMatrix());

		camera.position.set(0, 25.0f, height/2);
		camera.zoom = 0.12f;
		camera.update();

		environment = new Environment();
		environment.set(new ColorAttribute(ColorAttribute.AmbientLight, .4f, .4f, .4f, 1f));
		environment.add(new DirectionalLight().set(0.8f, 0.8f, 0.8f, -0.5f, -1f, 1f));

		model = game.getModelBuilder().createBox(10f, 5f, 10f,
				new Material(ColorAttribute.createDiffuse(Color.BLUE)),
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
		modelInstance = new ModelInstance(model);
	}

	@Override
	public void draw(Batch batch, float parentAlpha) {
		batch.end();
		shapeRenderer.end();

		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		shapeRenderer.setProjectionMatrix(batch.getProjectionMatrix());
		shapeRenderer.setTransformMatrix(batch.getTransformMatrix());
		//shapeRenderer.rect(getX(), getY(), 50, 50);

		/*
		for (int i = 0; i < level.getRows(); i++) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!level.getMatrix()[i][j].isDead()) {
					//level.getMatrix()[i][j].draw(getX() - width/2 + j*50.0f,
					//		getY() - height/2 + i*50.0f, shapeRenderer);
					*/

		/*
		for (int i = level.getRows() - 1; i >= 0; i--) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!level.getMatrix()[i][j].isDead()) {
					spriteBatch.draw(texure, getX() - width/2 + j*72.0f + i*26.0f, getY() - height/2 + i*61.0f - j*22.0f);
				}
			}
		}
		*/



		Gdx.gl.glViewport(0, 0, Gdx.graphics.getWidth(), Gdx.graphics.getHeight());
		Gdx.gl.glClear(GL20.GL_COLOR_BUFFER_BIT | GL20.GL_DEPTH_BUFFER_BIT);

		game.getModelBatch().begin(camera);
		//game.getModelBatch().render(modelInstance);

		for (int i = 0; i < level.getRows(); i++) {
			for (int j = 0; j < level.getColumns(); j++) {
				if (!level.getMatrix()[i][j].isDead()) {
					/*
					ModelInstance tileInstance = new ModelInstance(model,
							-(width/2) + j*10.0f, 0, (height/2) - i*10.0f);
					game.getModelBatch().render(tileInstance, environment);
					*/
					game.getModelBatch().render(matrix[i][j], environment);
				}
			}
		}
		game.getModelBatch().end();

		//player.draw(getX() - width/2, getY() - height/2, shapeRenderer);
		shapeRenderer.end();
		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		batch.begin();
	}

	private ModelInstance[][] parseMatrix(Tile[][] tileMatrix) {
		ModelInstance[][] modelMatrix =
				new ModelInstance[tileMatrix.length][tileMatrix[0].length];

		for (int i = 0; i < tileMatrix.length; i++) {
			for (int j = 0; j < tileMatrix[0].length; j++) {
				if (tileMatrix[i][j] instanceof KeyTile) {
					KeyTile keyTile = (KeyTile) tileMatrix[i][j];
					modelMatrix[i][j] = new ModelInstance(
							game.getKeyTileModel(keyTile.getColor()));
				} else if (tileMatrix[i][j] instanceof LockTile) {
					LockTile lockTile = (LockTile) tileMatrix[i][j];
					modelMatrix[i][j] = new ModelInstance(
							game.getLockTileModel(lockTile.getColor()));
				} else if (tileMatrix[i][j] instanceof ExitTile) {
					modelMatrix[i][j] = new ModelInstance(
							game.getExitTileModel());
				} else {
					modelMatrix[i][j] = new ModelInstance(
							game.getTileModel());
				}
				modelMatrix[i][j].transform.setToTranslation(
						-width/2 + j*10.0f, 0, height/2 - i*10.0f);
			}
		}

		return modelMatrix;
	}

}
