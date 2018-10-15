package com.example.viveksni.androidrcremote;

import android.os.AsyncTask;
import android.os.Bundle;
import android.support.v4.app.Fragment;
import android.view.LayoutInflater;
import android.view.MotionEvent;
import android.view.View;
import android.view.ViewGroup;
import android.webkit.WebView;
import android.webkit.WebViewClient;
import android.widget.ImageButton;
import android.widget.ProgressBar;
import android.widget.Toast;


/**
 * A simple {@link Fragment} subclass.
 * Activities that contain this fragment must implement the
 * to handle interaction events.
 * Use the {@link ControlFragment#newInstance} factory method to
 * create an instance of this fragment.
 */
public class ControlFragment extends Fragment {
    // TODO: Rename parameter arguments, choose names that match
    // the fragment initialization parameters, e.g. ARG_ITEM_NUMBER
    ImageButton forward_btn, forward_left_btn, stop_btn, forward_right_btn, reverse_btn, disconnect_btn;
    ImageButton tiltup_btn, tiltdown_btn, tiltright_btn, tiltleft_btn, center_btn;
    ImageButton camera_btn, horn_btn, automode_btn, cameralight_btn;

    String command; //string variable that will store value to be transmitted to the bluetooth module
    public String hostIpAddress;
    private WebView mWebView = null;
    AsyncTaskRunner asyncTaskRunner;
    ProgressBar progressBar;
    private boolean iscamon, isAutoMode, isCamLightOn = false;

    //private OnFragmentInteractionListener mListener;

    public ControlFragment() {
        // Required empty public constructor
    }

    public static ControlFragment newInstance() {
        ControlFragment fragment = new ControlFragment();
        return fragment;
    }

    @Override
    public void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
    }

    @Override
    public View onCreateView(LayoutInflater inflater, ViewGroup container,
                             final Bundle savedInstanceState) {
        // Inflate the layout for this fragment
        View rootView = inflater.inflate(R.layout.fragment_control, container, false);
        mWebView = (WebView) rootView.findViewById(R.id.feedview);
        progressBar = rootView.findViewById(R.id.progressBar);
        mWebView.getSettings().setJavaScriptEnabled(true);
        mWebView.setWebViewClient(new ControlFragment.AppWebViewClients(progressBar));
        mWebView.stopLoading();

        //declaration of button variables
        forward_btn = (ImageButton) rootView.findViewById(R.id.fwd_btn);
        forward_left_btn = (ImageButton) rootView.findViewById(R.id.left_btn);
        forward_right_btn = (ImageButton) rootView.findViewById(R.id.right_btn);
        reverse_btn = (ImageButton) rootView.findViewById(R.id.rev_btn);
        stop_btn = (ImageButton) rootView.findViewById(R.id.stop_btn);
        disconnect_btn = (ImageButton) rootView.findViewById(R.id.disconnect_btn);
        camera_btn = (ImageButton) rootView.findViewById(R.id.camera_btn);
        horn_btn = (ImageButton) rootView.findViewById(R.id.btn_horn);
        automode_btn = (ImageButton) rootView.findViewById(R.id.btn_auto);
        cameralight_btn= (ImageButton) rootView.findViewById(R.id.btn_camlighton);

        tiltdown_btn = (ImageButton) rootView.findViewById(R.id.tiltdown_btn);
        tiltleft_btn = (ImageButton) rootView.findViewById(R.id.tiltleft_btn);
        tiltright_btn = (ImageButton) rootView.findViewById(R.id.titlright_btn);
        tiltup_btn = (ImageButton) rootView.findViewById(R.id.tiltup_btn);
        center_btn = (ImageButton) rootView.findViewById(R.id.center_btn);

        forward_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View view, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) //MotionEvent.ACTION_DOWN is when you hold a button down
                {
                    command = "DRIVE-2";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) //MotionEvent.ACTION_UP is when you release a button
                {
                    command = "DRIVE-1";
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
                    command = "DRIVE-3";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    command = "DRIVE-1";
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
                    command = "DRIVE-4";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    command = "DRIVE-1";
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
                    command = "DRIVE-5";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command);
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    command = "DRIVE-1";
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
                command = "DRIVE-1";
                sendCommand(command);

                return false;
            }
        });

        //OnTouchListener code for the forward right button (button long press)
        disconnect_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View v, MotionEvent event) {
                BluetoothService bluetooth = BluetoothService.getInstance();
                bluetooth.disConnectFromDevice();
                return false;
            }
        });

        tiltup_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View view, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) //MotionEvent.ACTION_DOWN is when you hold a button down
                {
                    command = "TILT-1";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command, "1000");
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    asyncTaskRunner.cancel(true);
                }
                return false;
            }
        });

        tiltdown_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View view, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) //MotionEvent.ACTION_DOWN is when you hold a button down
                {
                    command = "TILT-2";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command, "1000");
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    asyncTaskRunner.cancel(true);
                }
                return false;
            }
        });

        tiltleft_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View view, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) //MotionEvent.ACTION_DOWN is when you hold a button down
                {
                    command = "TILT-3";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command, "1000");
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    asyncTaskRunner.cancel(true);
                }
                return false;
            }
        });

        tiltright_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View view, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) //MotionEvent.ACTION_DOWN is when you hold a button down
                {
                    command = "TILT-4";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command, "1000");
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    asyncTaskRunner.cancel(true);
                }
                return false;
            }
        });

        center_btn.setOnTouchListener(new View.OnTouchListener() {
            @Override
            public boolean onTouch(View view, MotionEvent event) {
                if (event.getAction() == MotionEvent.ACTION_DOWN) //MotionEvent.ACTION_DOWN is when you hold a button down
                {
                    command = "TILT-5";
                    asyncTaskRunner = new AsyncTaskRunner();
                    asyncTaskRunner.execute(command, "1000");
                } else if (event.getAction() == MotionEvent.ACTION_UP) {
                    asyncTaskRunner.cancel(true);
                }
                return false;
            }
        });

        automode_btn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                if (!isAutoMode) {
                    command = "DRIVE-AUTO";
                    isAutoMode = true;
                    automode_btn.setImageResource(R.drawable.imgbtn_autooff);
                } else {
                    command = "DRIVE-AUTOOFF";
                    isAutoMode = false;
                    automode_btn.setImageResource(R.drawable.imgbtn_autoon);
                }
                sendCommand(command);
                setDriveButtonsActivity();
            }
        });

        cameralight_btn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
                if (!isCamLightOn) {
                    command = "CAMERA-LED-ON";
                    isCamLightOn = true;
                    cameralight_btn.setImageResource(R.drawable.imgbtn_camlightoff);
                } else {
                    command = "CAMERA-LED-OFF";
                    isCamLightOn = false;
                    cameralight_btn.setImageResource(R.drawable.imgbtn_camlighton);
                }
                sendCommand(command);
            }
        });

        camera_btn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                if (!iscamon) {
                    command = "CAM-1";
                    camera_btn.setImageResource(R.drawable.imgbtn_cameraoff);
                } else {
                    command = "CAM-0";
                    camera_btn.setImageResource(R.drawable.imgbtn_cameraon);
                }
                sendCommand(command);
            }
        });

        horn_btn.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View v) {
                command = "HORN";
                sendCommand(command);
            }
        });

        return rootView;
    }

    public void reloadWebView(String message) {
        if (message.equalsIgnoreCase("CAMON")) {
            this.progressBar.setVisibility(View.VISIBLE);
            this.mWebView.loadUrl(this.getFeedUrl());
            this.mWebView.setVisibility(View.VISIBLE);
            this.iscamon = true;
        } else {
            this.mWebView.setVisibility(View.INVISIBLE);
            this.iscamon = false;
        }
    }

    public void sendVoiceCommand(String command) {
        if (command.contains("forward")) {
            sendCommand("DRIVE-2");
            DelayExecutor.delay(500, new DelayExecutor.DelayCallback() {
                @Override
                public void afterDelay() {
                    sendCommand("DRIVE-1");
                }
            });
        } else if (command.contains("back")) {
            sendCommand("DRIVE-3");
            DelayExecutor.delay(500, new DelayExecutor.DelayCallback() {
                @Override
                public void afterDelay() {
                    sendCommand("DRIVE-1");
                }
            });
        } else if (command.contains("turn left")) {
            sendCommand("DRIVE-4");
            DelayExecutor.delay(500, new DelayExecutor.DelayCallback() {
                @Override
                public void afterDelay() {
                    sendCommand("DRIVE-1");
                }
            });
        } else if (command.contains("turn right")) {
            sendCommand("DRIVE-5");
            DelayExecutor.delay(500, new DelayExecutor.DelayCallback() {
                @Override
                public void afterDelay() {
                    sendCommand("DRIVE-1");
                }
            });
        } else if (command.startsWith("message")) {
            sendCommand(command.substring(7).trim());
        } else {
            Toast.makeText(getActivity().getApplicationContext(), "Command not recognized: " + command, Toast.LENGTH_LONG).show();
        }
    }

    private void setDriveButtonsActivity() {
        forward_left_btn.setEnabled(!isAutoMode);
        forward_left_btn.setClickable(!isAutoMode);

        forward_right_btn.setEnabled(!isAutoMode);
        forward_right_btn.setClickable(!isAutoMode);

        forward_btn.setEnabled(!isAutoMode);
        forward_btn.setClickable(!isAutoMode);

        reverse_btn.setEnabled(!isAutoMode);
        reverse_btn.setClickable(!isAutoMode);
    }

    private String getFeedUrl() {
        return "http://" + hostIpAddress + "/";
    }

    private class AppWebViewClients extends WebViewClient {
        private ProgressBar progressBar;

        public AppWebViewClients(ProgressBar progressBar) {
            this.progressBar = progressBar;
        }

        @Override
        public boolean shouldOverrideUrlLoading(WebView view, String url) {
            progressBar.setVisibility(View.VISIBLE);
            view.loadUrl(url);
            return true;
        }

        @Override
        public void onPageFinished(WebView view, String url) {
            super.onPageFinished(view, url);
            progressBar.setVisibility(View.GONE);
        }
    }

    private void sendCommand(String command) {
        BluetoothService bluetooth = BluetoothService.getInstance();
        boolean sent = bluetooth.send(command);
    }

    private class AsyncTaskRunner extends AsyncTask<String, Void, Boolean> {

        @Override
        protected Boolean doInBackground(String... params) {
            try {
                String delay = "500";
                while (!isCancelled()) {
                    String command = params[0];
                    if (command == null) {
                        continue;
                    }
                    if (params.length > 1) {
                        delay = params[1];
                    }

                    sendCommand(command);
                    Thread.sleep(Integer.parseInt(delay));
                }
            } catch (Exception e) {
                e.printStackTrace();
            }

            return true;
        }
    }


// TODO: Rename method, update argument and hook method into UI event
//    public void onButtonPressed(Uri uri) {
//        if (mListener != null) {
//            mListener.onFragmentInteraction(uri);
//        }
//    }

//    @Override
//    public void onAttach(Context context) {
//        super.onAttach(context);
//        if (context instanceof OnFragmentInteractionListener) {
//            mListener = (OnFragmentInteractionListener) context;
//        } else {
//            throw new RuntimeException(context.toString()
//                    + " must implement OnFragmentInteractionListener");
//        }
//    }
//
//    @Override
//    public void onDetach() {
//        super.onDetach();
//        mListener = null;
//    }

/**
 * This interface must be implemented by activities that contain this
 * fragment to allow an interaction in this fragment to be communicated
 * to the activity and potentially other fragments contained in that
 * activity.
 * <p>
 * See the Android Training lesson <a href=
 * "http://developer.android.com/training/basics/fragments/communicating.html"
 * >Communicating with Other Fragments</a> for more information.
 */
//    public interface OnFragmentInteractionListener {
//        // TODO: Update argument type and name
//        void onFragmentInteraction(Uri uri);
//    }

}

