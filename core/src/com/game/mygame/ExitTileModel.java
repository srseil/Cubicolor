package com.game.mygame;

import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;

import java.util.ArrayList;

public class ExitTileModel extends ModelInstance
		implements Observer, AnimationController.AnimationListener {

	private enum State {
		STILL,
		MOVING_DOWN,
		MOVING_UP,
		SNAPPING
	}

	private ExitTile data;
	private State state;
	private AnimationController moveAnimation;

	private ArrayList<ModelInstance> requirementModels;
	private float x, z;
	private int row, column;
	private int height;

	public ExitTileModel(Model model, ExitTile data, float x, float z,
						 int row, int column) {
		super(model);
		//transform.scale(10, 10, 10);
		this.data = data;
		this.x = x;
		this.z = z;
		this.row = row;
		this.column = column;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
		state = State.STILL;
		height = data.getHeight();

		requirementModels = new ArrayList<>();
		requirementModels.add(null);
		requirementModels.add(null);

		updateTransform(height);
	}

	private void updateTransform(int height) {
		transform.setTranslation(x, (height+1) * TileModel.SIZE/2, z);
	}

	@Override
	public void updateState() {
		//requirement met during removing model? -> queuing
		if (state == State.STILL &&
				data.getRequirements().size() < requirementModels.size()) {
			moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
			state = State.MOVING_DOWN;
		}
		height = data.getHeight();
	}

	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Spiral")) {
			if (state == State.MOVING_DOWN) {
				requirementModels.remove(0);
				if (requirementModels.size() == 0) {
					state = State.SNAPPING;
					moveAnimation.setAnimation("Cube|Fall", 1, 1.0f, this);
				} else {
					state = State.STILL;
					moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
				}
				updateTransform(height);
			} else if (state == State.MOVING_UP) {
				requirementModels.add(null);
				if (requirementModels.size() == data.getRequirements().size()) {
					state = State.STILL;
					moveAnimation.setAnimation("Cube|Spiral", 1, 1.0f, this);
				} else {
					moveAnimation.setAnimation("Cube|Spiral", 1, -1.0f, this);
				}
				updateTransform(height);
			}
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (state == State.MOVING_UP) {
				moveAnimation.setAnimation("Cube|Spiral", 1, -1.0f, this);
				updateTransform(1);
			} else {
				state = State.STILL;
				moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	public void update(float delta) {
		switch (state) {
			case MOVING_DOWN:
				moveAnimation.update(delta);
				break;
			case MOVING_UP:
				moveAnimation.update(delta);
				break;
			case SNAPPING:
				moveAnimation.update(delta);
		}

	}

	public void reset() {
		// ganz ende, während snapping
		height = data.getHeight();
		if (data.getRequirements().size() == requirementModels.size()) {
			if (state == State.MOVING_DOWN) {
				// Das oder unten >=:
				requirementModels.remove(0); // Später unnötig
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			}
		} else {
			if (state == State.STILL) {
				if (requirementModels.size() == 0) {
					state = State.MOVING_UP;
					moveAnimation.setAnimation("Cube|Fall", 1, -1.0f, this);
				} else {
					updateTransform(height); // basierend auf differenz
					moveAnimation.setAnimation("Cube|Spiral", 1, -1.0f, this);
					state = State.MOVING_UP;
				}
			} else if (state == State.MOVING_DOWN) {
				requirementModels.remove(0); // Später unnötig
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			} else if (state == State.SNAPPING) {
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			}
		}
	}

	public int getRow() {
		return row;
	}

	public int getColumn() {
		return column;
	}

	public boolean isTraversable() {
		if (height == 0 && state == State.STILL)
			return true;
		else
			return false;
	}

}

