package com.example.viveksni.androidrcremote;

import android.content.Context;
import android.net.Uri;
import android.os.AsyncTask;
import android.os.Bundle;
import android.support.annotation.Nullable;
import android.support.v4.app.Fragment;
import android.view.LayoutInflater;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.widget.Button;

/**
 * A simple {@link Fragment} subclass.
 * Activities that contain this fragment must implement the
 * to handle interaction events.
 * Use the {@link CommandsFragment#newInstance} factory method to
 * create an instance of this fragment.
 */
public class CommandsFragment extends Fragment {

    Button forward_btn, forward_left_btn, stop_btn, forward_right_btn, reverse_btn;

    String command; //string variable that will store value to be transmitted to the bluetooth module


    // private OnFragmentInteractionListener mListener;

    public CommandsFragment() {
        // Required empty public constructor
    }

    /**
     * Use this factory method to create a new instance of
     * this fragment using the provided parameters.
     *
     * @return A new instance of fragment BTCommands.
     */
    // TODO: Rename and change types and number of parameters
    public static CommandsFragment newInstance() {
        CommandsFragment fragment = new CommandsFragment();
        return fragment;
    }

    AsyncTaskRunner asyncTaskRunner;


    @Nullable
    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
                             Bundle savedInstanceState) {
        View rootView = inflater.inflate(R.layout.fragment_commands, container, false);

        //declaration of button variables
        forward_btn = (Button) rootView.findViewById(R.id.forward_btn);
        forward_left_btn = (Button) rootView.findViewById(R.id.forward_left_btn);
        forward_right_btn = (Button) rootView.findViewById(R.id.forward_right_btn);
        reverse_btn = (Button) rootView.findViewById(R.id.reverse_btn);
        stop_btn = (Button) rootView.findViewById(R.id.stop_btn);

        forward_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View view, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) //MotionEvent.ACTION_DOWN is when you hold a button down
                {
                    command = "2";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) //MotionEvent.ACTION_UP is when you release a button
                {
                    command = "1";
                    asyncTaskRunner.cancel(true);
                    sendCommand(command);
                }

                return false;
            }
        });


        //OnTouchListener code for the reverse button (button long press)
        reverse_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) {
                    command = "3";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    command = "1";
                    asyncTaskRunner.cancel(true);
                    sendCommand(command);
                }
                return false;
            }
        });

        //OnTouchListener code for the forward left button (button long press)
        forward_left_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) {
                    command = "4";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    command = "1";
                    asyncTaskRunner.cancel(true);
                    sendCommand(command);
                }
                return false;
            }
        });

        //OnTouchListener code for the forward right button (button long press)
        forward_right_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) {
                    command = "5";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    command = "1";
                    asyncTaskRunner.cancel(true);
                    sendCommand(command);
                }
                return false;
            }
        });

        //OnTouchListener code for the forward right button (button long press)
        stop_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                command = "1";
                asyncTaskRunner.cancel(true);
                sendCommand(command);

                return false;
            }
        });
        return rootView;
    }

    private void sendCommand(String command) {
        BluetoothService bluetooth = BluetoothService.getInstance();
        boolean sent = bluetooth.send(command);
    }

    private class AsyncTaskRunner extends AsyncTask<String, Void, Boolean> {

        @Override
        protected Boolean doInBackground(String... params) {
            try {
                while (!isCancelled()) {
                    String command = params[0];
                    if (command == null) {
                        continue;
                    }
                    sendCommand(command);
                    Thread.sleep(500);
                }
            } catch (Exception e) {
                e.printStackTrace();
            }

            return true;
        }
    }
}
