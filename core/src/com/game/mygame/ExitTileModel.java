package com.game.mygame;

import com.badlogic.gdx.graphics.GL20;
import com.badlogic.gdx.graphics.g3d.Model;
import com.badlogic.gdx.graphics.g3d.ModelInstance;
import com.badlogic.gdx.graphics.g3d.attributes.IntAttribute;
import com.badlogic.gdx.graphics.g3d.utils.AnimationController;
import com.sun.org.apache.regexp.internal.RE;

import java.util.ArrayList;
import java.util.EnumSet;
import java.util.Iterator;

public class ExitTileModel extends ModelInstance
		implements Observer, AnimationController.AnimationListener {

	// Idea for requirement remove and add:
	// Save requirements as array, then traverse over it with height and add
	// the right one depending on the height.
	// Also get color of removed one somehow to remove the right one...

	// The speed the model's animations are played at.
	public static final float SPIRAL_SPEED = 1.5f;
	public static final float FALL_SPEED = 1.0f;

	/*
	 * The possible states a ExitTileModel can be in.
	 */
	private enum State {
		STILL,
		MOVING_DOWN,
		MOVING_UP,
		SNAPPING
	}

	private ExitTile data;
	private TileColor[] requirements;
	private State state;
	private AnimationController moveAnimation;

	private ArrayList<RequirementModel> requirementModels;
	private int livingModels;
	private int removedColor;
	private float x, z;
	private int row, column;
	private int height;
	private boolean onHold;

	public ExitTileModel(Model model, ExitTile data,
						 EnumSet<TileColor> requirements,
						 float x, float z, int row, int column, MyGame game) {
		super(model);

		//materials.first().set(new IntAttribute(IntAttribute.CullFace, GL20.GL_NONE));
		//IntAttribute i = (IntAttribute)materials.first().get(IntAttribute.CullFace);
		//i.value = GL20.GL_NONE;

		this.data = data;
		//this.requirements = (TileColor[]) requirements.toArray();
		this.x = x;
		this.z = z;
		this.row = row;
		this.column = column;
		moveAnimation = new AnimationController(this);
		moveAnimation.allowSameAnimation = true;
		moveAnimation.setAnimation("Cube|Spiral");
		//height = data.getHeight();
		height = 0;
		System.out.println(height);
		state = State.STILL;

		// Construct list of RequirementModels, top to bottom.
		requirementModels = new ArrayList<>();
		int h = data.getHeight() - 1;
		for (TileColor color : requirements) {
			System.out.println("Requirement: " + color);
			requirementModels.add(new RequirementModel(color, h, x, z, this, game));
			h--;
		}

		updateTransform(0);
	}

	/*
	 * Update the model's transform according to the height it its height.
	 */
	private void updateTransform(int height) {
		transform.setTranslation(x, (height+1) * TileModel.SIZE/2, z);
	}

	@Override
	public void updateState(Object... args) {
		// Calculate the index of the removed TileColor for requirementModels
		// and destroy it.
		TileColor removed = (TileColor) args[0];
		for (int i = 0; i < requirementModels.size(); i++) {
			if (requirementModels.get(i).isAlive() &&
					requirementModels.get(i).getColor().equals(removed)) {
				removedColor = i;
				break;
			}
		}
		System.out.println("Index: " + removedColor + " " + args[0]);
		requirementModels.get(removedColor).destroy();

		/*
		 * BELOW: Implement queuing!
		 */
		//requirement met during removing model? -> queuing
		// Update the state and animation.
		// if clause not necessary? why would it be different?
		if (state == State.STILL &&
				data.getRequirements().size() < requirementModels.size()) {
		}
		moveAnimation.setAnimation("Cube|Spiral", 1, SPIRAL_SPEED, this);
		state = State.MOVING_DOWN;


		height = data.getHeight();

		holdRequirements();
		hold();
	}

	/*
	 * Note that height is oftentimes one too much, because the reversed
	 * animations start at the bottom and thus at the wrong height.
	 */
	@Override
	public void onEnd(AnimationController.AnimationDesc animation) {
		if (animation.animation.id.equals("Cube|Spiral")) {
			if (state == State.MOVING_DOWN) {
				// Spiraling down.

				// Remove specific color...?
				//requirementModels.remove(0);
				//requirementModels.get(removedColor).destroy();
				livingModels--;
				if (livingModels == 0) {
					// Model is close above the board.
					System.out.println("SNAPPING MAN");
					state = State.SNAPPING;
					moveAnimation.setAnimation(
							"Cube|Fall", 1, FALL_SPEED, this);
				} else {
					// Model still has space to spiral down.
					state = State.STILL;
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, SPIRAL_SPEED, this);
				}
				updateTransform(height);
				System.out.println(height);
			} else if (state == State.MOVING_UP) {
				System.out.println("HEIGHT: " + height);
				// Spiraling up.
				//requirementModels.add();
				requirementModels.get(requirementModels.size() - livingModels - 1).revive();
				//requirementModels.get(livingModels).revive();
				livingModels++;
				if (livingModels == data.getRequirements().size()) {
					// Model has spiraled all the way up.
					System.out.println("HEIGHT: 1 choice");
					state = State.STILL;
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, SPIRAL_SPEED, this);
				} else {
					// Model still has to spiral further up.
					height++;
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, -SPIRAL_SPEED, this);
				}
				updateTransform(height);
			}
		} else if (animation.animation.id.equals("Cube|Fall")) {
			if (state == State.MOVING_UP) {
				// Model snaps out of board height.
				height = 1;
				moveAnimation.setAnimation(
						"Cube|Spiral", 1, -SPIRAL_SPEED, this);
				updateTransform(height);
			} else {
				// Model snapped to board height.
				state = State.STILL;
				moveAnimation.setAnimation("Cube|Fall", 1, -FALL_SPEED, this);
				System.out.println("SNAPPING_DONE: " + height);
			}
		}
	}

	@Override
	public void onLoop(AnimationController.AnimationDesc animation) {}

	/*
	 * Update the models' animations according to time passed since last frame.
	 */
	public void update(float delta) {
		if (onHold) {
			//System.out.println("onHold!");
			return;
		}

		/*
		for (RequirementModel model : requirementModels)
			model.update(delta);
			*/

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

	/*
	 * Initiate resetting the model to its default height.
	 */
	public void reset() {
		// ganz ende, während snapping
		//height = data.getHeight();
		if (livingModels == data.getRequirements().size()) {
			// Model is already at the correct height.
			if (state == State.MOVING_DOWN) {
				// Model is moving down; reverse movement.
				// Das oder unten >=:
				//requirementModels.get(0).destroy(); // Später unnötig
				height++;
				System.out.println("RIGHT BRANCH? " + height);
				livingModels--;
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			}
		} else {
			// Model is not at correct height yet.
			if (state == State.STILL) {
				// Model is standing still.
				if (livingModels == 0) {
					// Model is at bottom; initiate snapping animation.
					state = State.MOVING_UP;
					moveAnimation.setAnimation(
							"Cube|Fall", 1, -FALL_SPEED, this);
				} else {
					// Model is in the air; initiate spiraling animation.
					height++;
					updateTransform(height); // basierend auf differenz
					moveAnimation.setAnimation(
							"Cube|Spiral", 1, -SPIRAL_SPEED, this);
					state = State.MOVING_UP;
				}
			} else if (state == State.MOVING_DOWN) {
				// Model is currently moving down; reverse movement.
				//requirementModels.get(0).destroy(); // Später unnötig
				height++;
				livingModels--;
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			} else if (state == State.SNAPPING) {
				// Model is currently snapping to the board; reverse snapping.
				state = State.MOVING_UP;
				moveAnimation.current.speed *= -1;
			}
		}
	}

	/*
	 * Setup the model's state and animation to bring up the board.
	 */
	public void setup() {
		height = 1;
		livingModels = 0;
		state = State.MOVING_UP;
		moveAnimation.setAnimation("Cube|Fall", 1, -FALL_SPEED, this);
	}

	/*
	 * Set the model on hold until it manually gets released.
	 */
	public void hold() {
		onHold = true;
	}

	/*
	 * Release the model if it is on hold.
	 */
	public void release() {
		System.out.println("RELEASING");
		onHold = false;
	}

	/*
	 * Set the requirement models on hold until they manually get released.
	 */
	public void holdRequirements() {
		for (RequirementModel model : requirementModels)
			model.hold();
	}

	/*
	 * Release the requirement models if they are on hold.
	 */
	public void releaseRequirements() {
		for (RequirementModel model : requirementModels)
			model.release();
	}

	/*
	 * Check if the model is at the bottom and can be traversed by the player.
	 */
	public boolean isTraversable() {
		if (height == 0 && state == State.STILL)
			return true;
		else
			return false;
	}

	public ArrayList<RequirementModel> getRequirementModels() {
		return requirementModels;
	}

	public int getRow() {
		return row;
	}

	public int getColumn() {
		return column;
	}

}

