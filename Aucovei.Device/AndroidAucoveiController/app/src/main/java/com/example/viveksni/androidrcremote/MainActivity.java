package com.example.viveksni.androidrcremote;

import android.content.ActivityNotFoundException;
import android.content.Intent;
import android.content.SharedPreferences;
import android.os.Handler;
import android.os.Message;
import android.speech.RecognizerIntent;
import android.support.design.widget.TabLayout;
import android.support.design.widget.FloatingActionButton;
import android.support.design.widget.Snackbar;
import android.support.v4.app.FragmentTransaction;
import android.support.v7.app.AppCompatActivity;
import android.support.v7.widget.Toolbar;

import android.support.v4.app.Fragment;
import android.support.v4.app.FragmentManager;
import android.support.v4.app.FragmentPagerAdapter;
import android.support.v4.view.ViewPager;
import android.os.Bundle;
import android.util.Log;
import android.view.Menu;
import android.view.MenuItem;
import android.view.View;
import android.widget.Toast;

import java.util.ArrayList;
import java.util.Locale;

public class MainActivity extends AppCompatActivity {

    public static final String PREFS_NAME = "com.patricia.bluetoothremote.settings";
    private static final String TAG = "MainAcivity";
    private static final int REQ_CODE_SPEECH_INPUT = 100;

    /**
     * The {@link android.support.v4.view.PagerAdapter} that will provide
     * fragments for each of the sections. We use a
     * {@link FragmentPagerAdapter} derivative, which will keep every
     * loaded fragment in memory. If this becomes too memory intensive, it
     * may be best to switch to a
     * {@link android.support.v4.app.FragmentStatePagerAdapter}.
     */
    private SectionsPagerAdapter mSectionsPagerAdapter;

    /**
     * The {@link ViewPager} that will host the section contents.
     */
    //private ViewPager mViewPager;
    public static SharedPreferences sharedPreferences;
    private CameraFeedFragment cameraFeedFragment;
    private ControlFragment controlFragment;


    @Override
    protected void onCreate(Bundle savedInstanceState) {
        super.onCreate(savedInstanceState);
        setContentView(R.layout.activity_main);
        cameraFeedFragment = CameraFeedFragment.newInstance();
        controlFragment = ControlFragment.newInstance();

//        Toolbar toolbar = (Toolbar) findViewById(R.id.toolbar);
//        setSupportActionBar(toolbar);
        // Create the adapter that will return a fragment for each of the three
        // primary sections of the activity.
        //mSectionsPagerAdapter = new SectionsPagerAdapter(getSupportFragmentManager());

        // Set up the ViewPager with the sections adapter.
        //mViewPager = (ViewPager) findViewById(R.id.container);
        //mViewPager.setAdapter(mSectionsPagerAdapter);

        //final TabLayout tabLayout = (TabLayout) findViewById(R.id.tabs);
        // tabLayout.setupWithViewPager(mViewPager);

        final MovableFloatingActionButton movingFab = (MovableFloatingActionButton) findViewById(R.id.voicecommandbtn);
        movingFab.setOnClickListener(new View.OnClickListener() {
            @Override
            public void onClick(View view) {
//                Snackbar.make(view, "Replace with your own action", Snackbar.LENGTH_LONG)
//                        .setAction("Action", null).show();
                startVoiceInput();
            }
        });

        final ConnectFragment connectFragment = ConnectFragment.newInstance();
        final FragmentTransaction ft = getSupportFragmentManager().beginTransaction();
        ft.replace(R.id.fragment_container, connectFragment);
        ft.commit();

        BluetoothService bs = BluetoothService.getInstance();
        bs.registerNewHandlerCallback(new Handler.Callback() {
            @Override
            public boolean handleMessage(Message msg) {
                try {
                    Log.d(TAG, "got a notification in " + Thread.currentThread());
                    String toasttext = "";
                    if (msg.what == BluetoothService.MessageConstants.MESSAGE_CONNECTED) {
                        toasttext = "Connected to device: ";
                    } else if (msg.what == BluetoothService.MessageConstants.MESSAGE_UNABLE_TO_CONNECT) {
                        toasttext = "Unable to connected to device: ";
                        connectFragment.HideLoadingIndicator();
                    } else if (msg.what == BluetoothService.MessageConstants.MESSAGE_DISCONNECTED) {
                        toasttext = "Disconnected from device";
                        controlFragment.hostIpAddress = null;
                        FragmentTransaction ft = getSupportFragmentManager().beginTransaction();
                        ft.replace(R.id.fragment_container, connectFragment);
                        ft.commit();
                        movingFab.hide();
                    } else if (msg.what == BluetoothService.MessageConstants.MESSAGE_READ) {
                        if (msg.obj.toString().contains("hostip:")) {
                           connectFragment.HideLoadingIndicator();
                            controlFragment.hostIpAddress = msg.obj.toString().replace("hostip:", "");
                            FragmentTransaction ft = getSupportFragmentManager().beginTransaction();
                            ft.replace(R.id.fragment_container, controlFragment);
                            ft.commit();
                            movingFab.show();
                        }
                        else if (msg.obj.toString().contains("CAM")) {
                            controlFragment.reloadWebView(msg.obj.toString());
                        }
                        toasttext = "Reading from device: ";
                    } else if (msg.what == BluetoothService.MessageConstants.MESSAGE_TOAST) {
                        toasttext = "Info: ";
                    } else if (msg.what == BluetoothService.MessageConstants.MESSAGE_WRITE) {
                        // toasttext = "Sending to device: ";
                        toasttext = null;
                    }

                    if (toasttext != null) {
                        toasttext += msg.obj.toString();
                        Toast.makeText(getApplicationContext(), toasttext, Toast.LENGTH_LONG).show();
                    }
                } catch (Throwable t) {
                    Log.e(TAG, null, t);
                }

                return false;
            }
        });

        sharedPreferences = getSharedPreferences(PREFS_NAME, 0);
    }

    @Override
    public boolean onCreateOptionsMenu(Menu menu) {
        // Inflate the menu; this adds items to the action bar if it is present.
        getMenuInflater().inflate(R.menu.menu_main, menu);
        return true;
    }

    @Override
    public boolean onOptionsItemSelected(MenuItem item) {
        // Handle action bar item clicks here. The action bar will
        // automatically handle clicks on the Home/Up button, so long
        // as you specify a parent activity in AndroidManifest.xml.
        int id = item.getItemId();

        //noinspection SimplifiableIfStatement
        if (id == R.id.action_settings) {
            return true;
        }

        return super.onOptionsItemSelected(item);
    }

    private void startVoiceInput() {
        Intent intent = new Intent(RecognizerIntent.ACTION_RECOGNIZE_SPEECH);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE_MODEL, RecognizerIntent.LANGUAGE_MODEL_FREE_FORM);
        intent.putExtra(RecognizerIntent.EXTRA_LANGUAGE, Locale.getDefault());
        intent.putExtra(RecognizerIntent.EXTRA_PROMPT, "Hello, How can I help you?");
        try {
            startActivityForResult(intent, REQ_CODE_SPEECH_INPUT);
        } catch (ActivityNotFoundException a) {
            throw a;
        }
    }

    @Override
    protected void onActivityResult(int requestCode, int resultCode, Intent data) {
        super.onActivityResult(requestCode, resultCode, data);
        switch (requestCode) {
            case REQ_CODE_SPEECH_INPUT: {
                if (resultCode == RESULT_OK && null != data) {
                    ArrayList<String> result = data.getStringArrayListExtra(RecognizerIntent.EXTRA_RESULTS);
                    // Toast.makeText(getApplicationContext(), result.get(0), Toast.LENGTH_LONG).show();
                    controlFragment.sendVoiceCommand(result.get(0));
                }
                break;
            }
        }
    }

    /**
     * A {@link FragmentPagerAdapter} that returns a fragment corresponding to
     * one of the sections/tabs/pages.
     */
    public class SectionsPagerAdapter extends FragmentPagerAdapter {

        public SectionsPagerAdapter(FragmentManager fm) {
            super(fm);
        }

        @Override
        public Fragment getItem(int position) {
            // getItem is called to instantiate the fragment for the given page.
            // Return a ConnectFragment (defined as a static inner class below).
            switch (position) {
                case 0:
                default:
                    return ConnectFragment.newInstance();
                case 1:
                    return ControlFragment.newInstance();
                case 2:
                    return cameraFeedFragment;
            }

        }

        @Override
        public int getCount() {
            // Show 3 total pages.
            return 3;
        }

        @Override
        public CharSequence getPageTitle(int position) {
            switch (position) {
                case 0:
                    return "Connect";
                case 1:
                    return "Commands";
                case 2:
                    return "Video";
            }

            return null;
        }
    }
}