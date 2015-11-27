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
	private float width, height;

	private PerspectiveCamera camera;
	private ShapeRenderer shapeRenderer;

	public GameBoard(Level level, Player player, PerspectiveCamera camera, MyGame game, ShapeRenderer shapeRenderer) {
		this.game = game;

		environment = new Environment();
		environment.set(new ColorAttribute(ColorAttribute.AmbientLight, .4f, .4f, .4f, 1f));
		environment.add(new DirectionalLight().set(0.8f, 0.8f, 0.8f, -1f, -0.8f, -0.2f));

		this.camera = camera;
		/*
		this.camera = camera;
		camera.position.set(20.0f, 20.0f, 20.0f);
		camera.lookAt(0, 0, 0);
		camera.near = 1f;
		camera.far = 300f;
		camera.update();
		*/

		model = game.getModelBuilder().createBox(5f, 5f, 5f,
				new Material(ColorAttribute.createDiffuse(Color.BLUE)),
				VertexAttributes.Usage.Position
				| VertexAttributes.Usage.Normal);
		modelInstance = new ModelInstance(model);



		this.level = level;
		this.player = player;
		this.shapeRenderer = shapeRenderer;

		width = (float) level.getColumns() * 73;
		height = (float) level.getRows() * 61;
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
		game.getModelBatch().render(modelInstance);
		game.getModelBatch().end();


		player.draw(getX() - width/2, getY() - height/2, shapeRenderer);
		shapeRenderer.end();
		shapeRenderer.begin(ShapeRenderer.ShapeType.Filled);
		batch.begin();
	}

}
